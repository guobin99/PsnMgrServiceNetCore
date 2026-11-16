using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PsnMgrService.Models;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.MicroApi;

/// <summary>
/// 考勤申请（移动端，对应 Net 版 MobileAttendanceApplyController）：补签 / 请假 / 申诉统一写 WX_KQ_Apply。
///   1) 不直接写 KQ_SupplyTime / KQ_Leave —— 未审批的数据不得进入考勤计算；
///   2) 三类申请共用一张表、一套状态机（0待审批 / 1已通过 / 2已驳回 / 3已撤回）；
///   3) 审批通过后的回写与重算在管理端完成。
/// </summary>
[ApiController]
[Authorize]
[Route("api/m/attapply")]
public class MobileAttendanceApplyController : ControllerBase
{
    private const string SelectApply = @"
SELECT ID_KEY, Apply_No, Person_ID, Apply_Type, Supply_Date, Supply_Time, Supply_Class,
       Kind_No, Kind_Name, Leave_Bgn, Leave_End, Leave_Hour, Leave_Days,
       Appeal_Date, Appeal_Type, Appeal_Time, Reason, CAST(Status AS int) AS Status,
       Audit_User, Audit_Time, Audit_Remark, Target_Table, Target_Ids, Create_Time
FROM WX_KQ_Apply";

    private readonly AppDbContext _db;

    public MobileAttendanceApplyController(AppDbContext db)
    {
        _db = db;
    }

    private int Pid => int.TryParse(User.FindFirst("PersonId")?.Value, out var v) ? v : 0;

    /// <summary>我的考勤申请（补签 + 请假 + 申诉，按提交时间倒序）</summary>
    [HttpGet("list")]
    public IActionResult List()
    {
        var rows = _db.Database.SqlQueryRaw<ApplyRow>(
            SelectApply + " WHERE Person_ID = @p0 ORDER BY Create_Time DESC",
            new SqlParameter("@p0", Pid)).ToList();

        return Ok(rows.Select(r => new
        {
            id = r.ID_KEY.ToString(),
            applyNo = r.Apply_No ?? "",
            type = KqApplyHelper.TypeText(r.Apply_Type),
            title = TitleOf(r),
            dateRange = RangeOf(r),
            reason = r.Reason ?? "",
            status = KqApplyHelper.StatusKey(r.Status),
            statusText = KqApplyHelper.StatusText(r.Status),
            submitTime = r.Create_Time.ToString("yyyy-MM-dd HH:mm"),
            // 只有「待审批」允许申请人撤回
            canCancel = r.Status == 0,
            auditUser = r.Audit_User ?? "",
            auditTime = r.Audit_Time.HasValue ? r.Audit_Time.Value.ToString("yyyy-MM-dd HH:mm") : "",
            auditRemark = r.Audit_Remark ?? ""
        }).ToList());
    }

    /// <summary>本月已提交的补签申请数（与提交时校验同一口径：待审批 + 已通过）</summary>
    [HttpGet("resign-count")]
    public IActionResult ResignCount()
    {
        int count = KqApplyHelper.CountMonthlyResign(Pid);
        return Ok(new { count, limit = KqApplyHelper.MonthlyResignLimit });
    }

    /// <summary>提交补签申请 → WX_KQ_Apply（待审批，不直接写 KQ_SupplyTime）</summary>
    [HttpPost("resign")]
    public IActionResult Resign([FromBody] ResignReq req)
    {
        if (req == null) return Ok(new { success = false, msg = "参数错误" });
        string? applyNo = KqApplyHelper.SubmitResign(Pid, req.date, req.time, req.clockType, req.reason,
            string.IsNullOrEmpty(req.scene) ? "h5" : req.scene, out var msg);
        if (applyNo == null) return Ok(new { success = false, msg });
        return Ok(new
        {
            success = true,
            applyNo,
            used = KqApplyHelper.CountMonthlyResign(Pid),
            limit = KqApplyHelper.MonthlyResignLimit
        });
    }

    /// <summary>提交请假申请 → WX_KQ_Apply（允许跨天，保留起止日期与时间段）</summary>
    [HttpPost("leave")]
    public IActionResult Leave([FromBody] ResignLeaveReq req)
    {
        if (req == null) return Ok(new { success = false, msg = "参数错误" });
        string? applyNo = KqApplyHelper.SubmitLeave(Pid, req.kindNo, req.timeBgn, req.timeEnd, req.remark,
            string.IsNullOrEmpty(req.scene) ? "h5" : req.scene, out var msg);
        if (applyNo == null) return Ok(new { success = false, msg });
        return Ok(new { success = true, applyNo });
    }

    /// <summary>提交考勤异常申诉 → WX_KQ_Apply（审批通过后由后台自动补签）</summary>
    [HttpPost("appeal")]
    public IActionResult Appeal([FromBody] AppealReq req)
    {
        if (req == null) return Ok(new { success = false, msg = "参数错误" });
        string? applyNo = KqApplyHelper.SubmitAppeal(Pid, req.date, req.appealType, req.appealTime, req.reason,
            string.IsNullOrEmpty(req.scene) ? "h5" : req.scene, out var msg);
        if (applyNo == null) return Ok(new { success = false, msg });
        return Ok(new { success = true, applyNo });
    }

    /// <summary>撤回申请：仅「待审批」可撤回（撤回后不占用补签月限）</summary>
    [HttpPost("cancel")]
    public IActionResult Cancel([FromBody] ApplyCancelReq req)
    {
        if (req == null || string.IsNullOrEmpty(req.applyNo))
            return Ok(new { success = false, msg = "申请号不能为空" });

        var row = _db.Database.SqlQueryRaw<ApplyRow>(
            SelectApply + " WHERE Apply_No = @p0",
            new SqlParameter("@p0", req.applyNo)).FirstOrDefault();

        if (row == null) return Ok(new { success = false, msg = "申请不存在" });
        if (row.Person_ID != Pid) return Ok(new { success = false, msg = "无权操作该申请" });
        if (row.Status != 0) return Ok(new { success = false, msg = "只有待审批的申请可以撤回" });

        _db.Database.ExecuteSqlRaw(
            "UPDATE WX_KQ_Apply SET Status = 3, Audit_Time = GETDATE(), Audit_Remark = N'申请人撤回' WHERE Apply_No = @p0 AND Status = 0",
            new SqlParameter("@p0", req.applyNo));
        return Ok(new { success = true });
    }

    // ================= 展示辅助 =================

    private static string TitleOf(ApplyRow r)
    {
        switch (r.Apply_Type)
        {
            case KqApplyHelper.TypeResign:
                return (r.Supply_Class == 2 ? "下班" : "上班") + "补签";
            case KqApplyHelper.TypeLeave:
                return "请假（" + (string.IsNullOrEmpty(r.Kind_Name) ? (r.Kind_No ?? "") : r.Kind_Name) + "）";
            default:
                return string.IsNullOrEmpty(r.Appeal_Type) ? "考勤申诉" : r.Appeal_Type!;
        }
    }

    private static string RangeOf(ApplyRow r)
    {
        switch (r.Apply_Type)
        {
            case KqApplyHelper.TypeResign:
                return ((r.Supply_Date ?? "") + " " + (r.Supply_Time ?? "")).Trim();
            case KqApplyHelper.TypeLeave:
                string range = (r.Leave_Bgn ?? "") + " ~ " + (r.Leave_End ?? "");
                if (r.Leave_Days.HasValue && r.Leave_Days.Value > 1) range += string.Format("（{0}天）", r.Leave_Days.Value);
                return range;
            default:
                return ((r.Appeal_Date ?? "") + " " + (r.Appeal_Time ?? "")).Trim();
        }
    }

    public class ApplyRow
    {
        public long ID_KEY { get; set; }
        public string? Apply_No { get; set; }
        public int Person_ID { get; set; }
        public string? Apply_Type { get; set; }
        public string? Supply_Date { get; set; }
        public string? Supply_Time { get; set; }
        public short? Supply_Class { get; set; }
        public string? Kind_No { get; set; }
        public string? Kind_Name { get; set; }
        public string? Leave_Bgn { get; set; }
        public string? Leave_End { get; set; }
        public double? Leave_Hour { get; set; }
        public int? Leave_Days { get; set; }
        public string? Appeal_Date { get; set; }
        public string? Appeal_Type { get; set; }
        public string? Appeal_Time { get; set; }
        public string? Reason { get; set; }
        public int Status { get; set; }
        public string? Audit_User { get; set; }
        public DateTime? Audit_Time { get; set; }
        public string? Audit_Remark { get; set; }
        public string? Target_Table { get; set; }
        public string? Target_Ids { get; set; }
        public DateTime Create_Time { get; set; }
    }
}

public class ResignReq
{
    public string? date { get; set; }
    public string? time { get; set; }
    public string? clockType { get; set; }
    public string? reason { get; set; }
    public string? scene { get; set; }
}

public class ResignLeaveReq
{
    public string? kindNo { get; set; }
    public string? timeBgn { get; set; }
    public string? timeEnd { get; set; }
    public string? remark { get; set; }
    public string? scene { get; set; }
}

public class AppealReq
{
    public string? date { get; set; }
    public string? appealType { get; set; }
    public string? appealTime { get; set; }
    public string? reason { get; set; }
    public string? scene { get; set; }
}

public class ApplyCancelReq { public string? applyNo { get; set; } }
