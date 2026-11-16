using Microsoft.Data.SqlClient;

namespace PsnMgrService.Services;

/// <summary>
/// 充值订单落库服务（对应 Net 版 PayOrderService）。
/// 成功：ST_Card.Card_Balance 增加 + ST_SupplyFund 写入（Out_Trade_No 关联）+ 站内消息；
/// 全程：WX_RechargeLog 记录每一笔（无论成败）。
/// </summary>
public static class PayOrderService
{
    public class RechargeLogInfo
    {
        public int? PersonId { get; set; }
        public decimal? Fund { get; set; }
        public string? Status { get; set; }
    }

    /// <summary>支付成功入账（幂等：同一 Out_Trade_No 只入账一次）</summary>
    public static string MarkPaid(int personId, string? outTradeNo, string? transactionId, decimal fund, string remark)
    {
        if (string.IsNullOrWhiteSpace(outTradeNo))
        {
            UpdateLog(personId, outTradeNo, transactionId, fund, "NO_TRADE_NO", null, "商户单号为空");
            return "NO_TRADE_NO";
        }
        if (fund <= 0)
        {
            UpdateLog(personId, outTradeNo, transactionId, fund, "BAD_AMOUNT", null, "入账金额无效");
            return "BAD_AMOUNT";
        }
        if (personId <= 0)
        {
            UpdateLog(null, outTradeNo, transactionId, fund, "NO_PERSON", null, "无法确定人员");
            return "NO_PERSON";
        }

        try
        {
            using var cn = new SqlConnection(AppConfig.ConnectionString);
            cn.Open();
            using var tx = cn.BeginTransaction();

            // Out_Trade_No 为幂等键。UPDLOCK+HOLDLOCK：并发到达的「异步通知」与「前端查询兜底」
            // 只能有一个进入入账分支，否则两边都查到 0 会重复给 ST_Card 加钱。
            using (var dupCmd = new SqlCommand(
                "SELECT COUNT(1) FROM ST_SupplyFund WITH (UPDLOCK, HOLDLOCK) WHERE Out_Trade_No = @p0", cn, tx))
            {
                dupCmd.Parameters.AddWithValue("@p0", outTradeNo);
                var dup = Convert.ToInt32(dupCmd.ExecuteScalar() ?? 0);
                if (dup > 0)
                {
                    tx.Commit();
                    UpdateLog(personId, outTradeNo, transactionId, fund, "SUCCESS", null, null);
                    return "OK";
                }
            }

            decimal before;
            using (var q = new SqlCommand("SELECT Card_Balance FROM ST_Card WHERE Person_ID = @p0", cn, tx))
            {
                q.Parameters.AddWithValue("@p0", personId);
                var v = q.ExecuteScalar();
                if (v == null || v == DBNull.Value)
                {
                    tx.Rollback();
                    UpdateLog(personId, outTradeNo, transactionId, fund, "NO_CARD", null, "未找到帐户");
                    return "NO_CARD";
                }
                before = Convert.ToDecimal(v);
            }

            decimal after = before + fund;
            using (var u = new SqlCommand(
                "UPDATE ST_Card SET Card_Balance = @b, Update_Time = GETDATE() WHERE Person_ID = @p0", cn, tx))
            {
                u.Parameters.AddWithValue("@b", after);
                u.Parameters.AddWithValue("@p0", personId);
                u.ExecuteNonQuery();
            }

            using (var ins = new SqlCommand(
                @"INSERT INTO ST_SupplyFund(Person_ID,Supply_Date,Supply_Fund,Card_Balance,MOC_No,Data_Type,User_No,Remark,Out_Trade_No)
                  VALUES(@p,@t,@f,@cb,'WX',1,'WECHAT',@rk,@o)", cn, tx))
            {
                ins.Parameters.AddWithValue("@p", personId);
                ins.Parameters.AddWithValue("@t", DateTime.Now);
                ins.Parameters.AddWithValue("@f", fund);
                ins.Parameters.AddWithValue("@cb", before);   // 充值前余额
                ins.Parameters.AddWithValue("@rk", remark ?? "");
                ins.Parameters.AddWithValue("@o", outTradeNo); // 关键：不写则幂等判重失效，通知+查询会重复入账
                ins.ExecuteNonQuery();
            }

            tx.Commit();
            UpdateLog(personId, outTradeNo, transactionId, fund, "SUCCESS", null, null);

            MobileMessageService.Push(personId, "recharge", "充值到账",
                string.Format("您的帐户成功充值 {0:0.00} 元，当前余额 {1:0.00} 元（单号 {2}）", fund, after, outTradeNo));
            return "OK";
        }
        catch (Exception ex)
        {
            UpdateLog(personId, outTradeNo, transactionId, fund, "ERROR", "EX", ex.Message);
            return "ERROR";
        }
    }

    public static void UpdateLog(int? personId, string? outTradeNo, string? txnId, decimal? fund,
        string? status, string? errCode, string? errMsg,
        string? rawSend = null, string? rawRecv = null, string? channel = null, string? tradeType = null)
    {
        try
        {
            using var cn = new SqlConnection(AppConfig.ConnectionString);
            using var cmd = new SqlCommand(
                @"IF EXISTS(SELECT 1 FROM WX_RechargeLog WHERE Out_Trade_No=@o)
                    UPDATE WX_RechargeLog SET Person_ID=ISNULL(@p,Person_ID),Status=@st,Transaction_Id=ISNULL(@t,Transaction_Id),
                      Total_Fee=ISNULL(@f,Total_Fee),Err_Code=ISNULL(@ec,Err_Code),Err_Msg=ISNULL(@em,Err_Msg),
                      Trade_Type=ISNULL(@tt,Trade_Type),Channel=ISNULL(@ch,Channel),
                      Raw_Send=ISNULL(@rs,Raw_Send),Raw_Recv=ISNULL(@rr,Raw_Recv),Update_Time=GETDATE()
                    WHERE Out_Trade_No=@o
                  ELSE
                    INSERT INTO WX_RechargeLog(Person_ID,Out_Trade_No,Transaction_Id,Total_Fee,Status,Err_Code,Err_Msg,Trade_Type,Channel,Raw_Send,Raw_Recv)
                    VALUES(@p,@o,@t,@f,@st,@ec,@em,@tt,@ch,@rs,@rr)", cn);

            cmd.Parameters.AddWithValue("@p", (object?)personId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@o", outTradeNo ?? "");
            cmd.Parameters.AddWithValue("@t", (object?)txnId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@f", (object?)fund ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@st", status ?? "");
            cmd.Parameters.AddWithValue("@ec", (object?)errCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@em", (object?)errMsg ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tt", (object?)tradeType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ch", (object?)channel ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@rs", (object?)rawSend ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@rr", (object?)rawRecv ?? DBNull.Value);
            cn.Open();
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    public static RechargeLogInfo? GetLog(string? outTradeNo)
    {
        try
        {
            using var cn = new SqlConnection(AppConfig.ConnectionString);
            using var cmd = new SqlCommand(
                "SELECT TOP(1) Person_ID,Total_Fee,Status FROM WX_RechargeLog WHERE Out_Trade_No=@o ORDER BY ID_KEY DESC", cn);
            cmd.Parameters.AddWithValue("@o", outTradeNo ?? "");
            cn.Open();
            using var rd = cmd.ExecuteReader();
            if (rd.Read())
            {
                return new RechargeLogInfo
                {
                    PersonId = rd.IsDBNull(0) ? null : rd.GetInt32(0),
                    Fund = rd.IsDBNull(1) ? null : rd.GetDecimal(1),
                    Status = rd.IsDBNull(2) ? "" : rd.GetString(2)
                };
            }
        }
        catch { }
        return null;
    }

    /// <summary>今日已入账的微信充值金额（单日限额校验用）</summary>
    public static decimal GetTodayRecharged(int personId)
    {
        try
        {
            using var cn = new SqlConnection(AppConfig.ConnectionString);
            using var cmd = new SqlCommand(
                "SELECT ISNULL(SUM(Supply_Fund),0) FROM ST_SupplyFund " +
                "WHERE Person_ID = @p0 AND MOC_No = 'WX' AND Supply_Date >= CONVERT(date, GETDATE())", cn);
            cmd.Parameters.AddWithValue("@p0", personId);
            cn.Open();
            var v = cmd.ExecuteScalar();
            return v == null || v == DBNull.Value ? 0m : Convert.ToDecimal(v);
        }
        catch
        {
            // 统计失败不阻断充值，只跳过单日限额校验
            return 0m;
        }
    }
}
