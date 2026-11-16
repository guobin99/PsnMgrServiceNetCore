using Microsoft.Data.SqlClient;

namespace PsnMgrService.Services;

/// <summary>
/// 考勤申请公共逻辑（对应 Net 版 KqApplyHelper）：补签 / 请假 / 申诉。
///   - 三类申请统一写入审批表 WX_KQ_Apply（Status=0 待审批），**不直接写厂商表**；
///   - 审批通过后由管理端 Admin_KQ_ApplyController 写 KQ_SupplyTime / KQ_Leave 并触发重算；
///   - 补签月限 3 次按「提交时间所在月份」统计，只统计未驳回（0 待审批 / 1 已通过）。
/// </summary>
public static class KqApplyHelper
{
    public const int MonthlyResignLimit = 3;
    public const int MaxLeaveDays = 30;

    public const string TypeResign = "resign";
    public const string TypeLeave = "leave";
    public const string TypeAppeal = "appeal";

    /// <summary>申请号：KQ + yyyyMMddHHmmss + 3 位随机（19 位，落在 varchar(30) 内）</summary>
    public static string NewApplyNo()
        => "KQ" + DateTime.Now.ToString("yyyyMMddHHmmss") + Random.Shared.Next(100, 1000);

    public static string StatusText(int status) => status switch
    {
        1 => "已通过",
        2 => "已驳回",
        3 => "已撤回",
        _ => "待审批"
    };

    public static string StatusKey(int status) => status switch
    {
        1 => "approved",
        2 => "rejected",
        3 => "cancelled",
        _ => "pending"
    };

    public static string TypeText(string? applyType) => applyType switch
    {
        TypeResign => "补签",
        TypeLeave => "请假",
        TypeAppeal => "申诉",
        _ => "申请"
    };

    // ================= 提交：补签 =================
    public static string? SubmitResign(int pid, string? date, string? time, string? clockType, string? reason,
        string? source, out string msg)
    {
        msg = "";
        date = (date ?? "").Trim();
        time = (time ?? "").Trim();
        if (date.Length == 0 || time.Length == 0) { msg = "请选择补签日期和时间"; return null; }

        if (!DateTime.TryParse(date, out var d)) { msg = "补签日期格式不正确"; return null; }
        d = d.Date;
        if (d > DateTime.Today) { msg = "补签日期不能晚于今天"; return null; }
        // 只允许补本月：与「按提交月份统计月限」口径保持一致，避免跨月补签绕过限额
        if (d < new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1))
        {
            msg = "只能补签本月内的日期";
            return null;
        }
        if (time.Length > 5) time = time.Substring(0, 5);

        short cls = clockType == "下班" ? (short)2 : (short)1;

        int used = CountMonthlyResign(pid);
        if (used >= MonthlyResignLimit)
        {
            msg = string.Format("本月补签申请已达上限（{0}次）", MonthlyResignLimit);
            return null;
        }

        // 同人 + 同日 + 同类型（上班/下班）只能有一条未驳回的申请
        int dup = ScalarInt(
            @"SELECT COUNT(1) FROM WX_KQ_Apply
              WHERE Person_ID = @p0 AND Apply_Type = 'resign'
                AND Supply_Date = @p1 AND Supply_Class = @p2 AND Status IN (0,1)",
            new SqlParameter("@p0", pid),
            new SqlParameter("@p1", d.ToString("yyyy-MM-dd")),
            new SqlParameter("@p2", cls));
        if (dup > 0) { msg = "该日期该类型已提交过补签申请，请勿重复提交"; return null; }

        return InsertApply(pid, TypeResign, source, reason ?? "",
            d.ToString("yyyy-MM-dd"), time, cls,
            null, null, null, null, 0, null,
            null, null, null);
    }

    /// <summary>某员工本月已提交的补签申请数（只算待审批 + 已通过）</summary>
    public static int CountMonthlyResign(int pid)
    {
        DateTime monthBgn = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        DateTime nextMonth = monthBgn.AddMonths(1);
        return ScalarInt(
            @"SELECT COUNT(1) FROM WX_KQ_Apply
              WHERE Person_ID = @p0 AND Apply_Type = 'resign'
                AND Status IN (0,1) AND Create_Time >= @p1 AND Create_Time < @p2",
            new SqlParameter("@p0", pid),
            new SqlParameter("@p1", monthBgn),
            new SqlParameter("@p2", nextMonth));
    }

    // ================= 提交：请假（允许跨天，保留首尾时间段） =================
    public static string? SubmitLeave(int pid, string? kindNo, string? timeBgn, string? timeEnd, string? remark,
        string? source, out string msg)
    {
        msg = "";
        kindNo = (kindNo ?? "").Trim();
        if (kindNo.Length == 0) { msg = "请选择请假类型"; return null; }

        if (!DateTime.TryParse(timeBgn, out var b) || !DateTime.TryParse(timeEnd, out var e))
        {
            msg = "请假时间格式错误";
            return null;
        }
        if (e <= b) { msg = "请假结束时间需晚于开始时间"; return null; }

        int days = (int)(e.Date - b.Date).TotalDays + 1;
        if (days > MaxLeaveDays) { msg = string.Format("单次请假不能超过 {0} 天", MaxLeaveDays); return null; }

        string kindName = ScalarString("SELECT TOP 1 Kind_Name FROM KQ_LeaveKind WHERE Kind_No = @p0",
            new SqlParameter("@p0", kindNo)) ?? kindNo;

        // 区间重叠校验（待审批 / 已通过都算占用）
        int dup = ScalarInt(
            @"SELECT COUNT(1) FROM WX_KQ_Apply
              WHERE Person_ID = @p0 AND Apply_Type = 'leave' AND Status IN (0,1)
                AND ISNULL(Leave_Bgn,'') <= @p2 AND ISNULL(Leave_End,'') >= @p1",
            new SqlParameter("@p0", pid),
            new SqlParameter("@p1", b.ToString("yyyy-MM-dd HH:mm")),
            new SqlParameter("@p2", e.ToString("yyyy-MM-dd HH:mm")));
        if (dup > 0) { msg = "该时间段已有请假申请（待审批或已通过），请勿重复提交"; return null; }

        double hours = Math.Round((e - b).TotalHours, 2);

        return InsertApply(pid, TypeLeave, source, remark ?? "",
            null, null, null,
            kindNo, kindName, b.ToString("yyyy-MM-dd HH:mm"), e.ToString("yyyy-MM-dd HH:mm"), hours, days,
            null, null, null);
    }

    // ================= 提交：申诉 =================
    public static string? SubmitAppeal(int pid, string? date, string? appealType, string? appealTime, string? reason,
        string? source, out string msg)
    {
        msg = "";
        date = (date ?? "").Trim();
        appealType = (appealType ?? "").Trim();
        if (date.Length == 0 || appealType.Length == 0) { msg = "请选择申诉日期与异常类型"; return null; }

        if (!DateTime.TryParse(date, out var d)) { msg = "申诉日期格式不正确"; return null; }
        d = d.Date;
        if (d > DateTime.Today) { msg = "申诉日期不能晚于今天"; return null; }
        if (d < DateTime.Today.AddDays(-90)) { msg = "只能申诉近 90 天内的考勤异常"; return null; }

        string t = (appealTime ?? "").Trim();
        if (t.Length > 5) t = t.Substring(0, 5);

        // 同人 + 同日 + 同异常类型只能有一条未驳回的申诉
        int dup = ScalarInt(
            @"SELECT COUNT(1) FROM WX_KQ_Apply
              WHERE Person_ID = @p0 AND Apply_Type = 'appeal'
                AND Appeal_Date = @p1 AND Appeal_Type = @p2 AND Status IN (0,1)",
            new SqlParameter("@p0", pid),
            new SqlParameter("@p1", d.ToString("yyyy-MM-dd")),
            new SqlParameter("@p2", appealType));
        if (dup > 0) { msg = "该日期该异常类型已提交过申诉，请勿重复提交"; return null; }

        return InsertApply(pid, TypeAppeal, source, reason ?? "",
            null, null, null,
            null, null, null, null, 0, null,
            d.ToString("yyyy-MM-dd"), appealType, t);
    }

    // ================= 落库 =================
    private static string InsertApply(int pid, string applyType, string? source, string reason,
        string? supplyDate, string? supplyTime, short? supplyClass,
        string? kindNo, string? kindName, string? leaveBgn, string? leaveEnd, double leaveHour, int? leaveDays,
        string? appealDate, string? appealType, string? appealTime)
    {
        string? personNo = null, personName = null;
        using (var cn = new SqlConnection(AppConfig.ConnectionString))
        {
            cn.Open();
            using var q = new SqlCommand("SELECT TOP 1 Person_No, Person_Name FROM ST_Person WHERE Person_ID = @p0", cn);
            q.Parameters.AddWithValue("@p0", pid);
            using var rd = q.ExecuteReader();
            if (rd.Read())
            {
                personNo = rd["Person_No"] == DBNull.Value ? null : Convert.ToString(rd["Person_No"]);
                personName = rd["Person_Name"] == DBNull.Value ? null : Convert.ToString(rd["Person_Name"]);
            }
        }

        if (reason.Length > 200) reason = reason.Substring(0, 200);
        string src = string.IsNullOrEmpty(source) ? "h5" : source!;
        if (src.Length > 10) src = src.Substring(0, 10);

        string applyNo = "";
        for (int i = 0; i < 3; i++)
        {
            string candidate = NewApplyNo();
            int exists = ScalarInt("SELECT COUNT(1) FROM WX_KQ_Apply WHERE Apply_No = @p0",
                new SqlParameter("@p0", candidate));
            if (exists == 0) { applyNo = candidate; break; }
        }
        if (applyNo.Length == 0) throw new InvalidOperationException("申请号生成失败");

        using var cn2 = new SqlConnection(AppConfig.ConnectionString);
        using var cmd = new SqlCommand(
            @"INSERT INTO WX_KQ_Apply
                (Apply_No, Person_ID, Person_No, Person_Name, Apply_Type,
                 Supply_Date, Supply_Time, Supply_Class,
                 Kind_No, Kind_Name, Leave_Bgn, Leave_End, Leave_Hour, Leave_Days,
                 Appeal_Date, Appeal_Type, Appeal_Time,
                 Reason, Status, Source, Create_Time)
              VALUES
                (@applyNo, @personId, @personNo, @personName, @applyType,
                 @supplyDate, @supplyTime, @supplyClass,
                 @kindNo, @kindName, @leaveBgn, @leaveEnd, @leaveHour, @leaveDays,
                 @appealDate, @appealType, @appealTime,
                 @reason, 0, @source, GETDATE())", cn2);
        cmd.Parameters.AddWithValue("@applyNo", applyNo);
        cmd.Parameters.AddWithValue("@personId", pid);
        cmd.Parameters.AddWithValue("@personNo", DbNull(personNo));
        cmd.Parameters.AddWithValue("@personName", DbNull(personName));
        cmd.Parameters.AddWithValue("@applyType", applyType);
        cmd.Parameters.AddWithValue("@supplyDate", DbNull(supplyDate));
        cmd.Parameters.AddWithValue("@supplyTime", DbNull(supplyTime));
        cmd.Parameters.AddWithValue("@supplyClass", DbNull(supplyClass));
        cmd.Parameters.AddWithValue("@kindNo", DbNull(kindNo));
        cmd.Parameters.AddWithValue("@kindName", DbNull(kindName));
        cmd.Parameters.AddWithValue("@leaveBgn", DbNull(leaveBgn));
        cmd.Parameters.AddWithValue("@leaveEnd", DbNull(leaveEnd));
        cmd.Parameters.AddWithValue("@leaveHour", leaveHour);
        cmd.Parameters.AddWithValue("@leaveDays", DbNull(leaveDays));
        cmd.Parameters.AddWithValue("@appealDate", DbNull(appealDate));
        cmd.Parameters.AddWithValue("@appealType", DbNull(appealType));
        cmd.Parameters.AddWithValue("@appealTime", DbNull(appealTime));
        cmd.Parameters.AddWithValue("@reason", DbNull(reason));
        cmd.Parameters.AddWithValue("@source", src);
        cn2.Open();
        cmd.ExecuteNonQuery();

        return applyNo;
    }

    // ---------------- 内部 ----------------

    private static int ScalarInt(string sql, params SqlParameter[] ps)
    {
        using var cn = new SqlConnection(AppConfig.ConnectionString);
        using var cmd = new SqlCommand(sql, cn);
        if (ps.Length > 0) cmd.Parameters.AddRange(ps);
        cn.Open();
        var v = cmd.ExecuteScalar();
        return v == null || v == DBNull.Value ? 0 : Convert.ToInt32(v);
    }

    private static string? ScalarString(string sql, params SqlParameter[] ps)
    {
        using var cn = new SqlConnection(AppConfig.ConnectionString);
        using var cmd = new SqlCommand(sql, cn);
        if (ps.Length > 0) cmd.Parameters.AddRange(ps);
        cn.Open();
        var v = cmd.ExecuteScalar();
        return v == null || v == DBNull.Value ? null : Convert.ToString(v);
    }

    private static object DbNull(object? v) => v ?? (object)DBNull.Value;
}
