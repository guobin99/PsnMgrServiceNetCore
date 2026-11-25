using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace PsnMgrService.Services;

/// <summary>
/// 卡状态机。ST_Card.Card_State 只有三个业务取值：0 正常、1 挂失、2 冻结。
/// 黑名单不是卡状态，由 ST_BlackPerson 独立记录（源码未写 Card_State = 9）。
/// </summary>
public static class CardState
{
    public const int Normal = 0;
    public const int Lost = 1;
    public const int Frozen = 2;

    public static string Text(int? state)
    {
        return state switch
        {
            Normal => "正常",
            Lost => "挂失",
            Frozen => "冻结",
            _ => "未知",
        };
    }

    /// <summary>当前状态下允许的业务动作，用于错误响应里告知前端可操作项。</summary>
    public static string[] AllowedActions(int? state)
    {
        return state switch
        {
            Normal => new[] { "freeze", "loss", "change", "close" },
            Lost => new[] { "unloss", "change", "close" },
            Frozen => new[] { "unfreeze", "close" },
            _ => Array.Empty<string>(),
        };
    }
}

/// <summary>
/// 卡密码与卡号加解密。算法与 CS 完全一致，不引入新算法：
/// 1) 卡密码 ST_Card.Card_Password = fnEncrypt(明文)，即 uDES.EncryStrHex(明文, 'suityou')：
///    标准 DES / ECB / 无 IV / 密钥取前 8 字节不足补 0 / 明文按 8 字节补 0 / 输出小写十六进制。
/// 2) 卡号加密 ST_Person.C_PSD = fnEncryptCardNo(卡号)，即 EncdDecd.EncodeString：
///    逐字符取字节转两位大写十六进制。
/// 明文按 ANSI（GBK）编码，与 Delphi AnsiString 的 Ord 取值一致；密码限定 ASCII 可打印字符。
/// </summary>
public static class CardCrypto
{
    private const string DesKey = "suityou";
    private static Encoding? _ansi;

    private static Encoding Ansi
    {
        get
        {
            if (_ansi != null) return _ansi;
            try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); } catch { /* 已注册 */ }
            try { _ansi = Encoding.GetEncoding(936); }
            catch { _ansi = Encoding.Latin1; }
            return _ansi;
        }
    }

    private static byte[] DesTransform(byte[] data, bool encrypt)
    {
        var key = new byte[8];
        var raw = Encoding.ASCII.GetBytes(DesKey);
        Array.Copy(raw, key, Math.Min(8, raw.Length));

        using var des = DES.Create();
        des.Key = key;
        des.Mode = CipherMode.ECB;
        des.Padding = PaddingMode.None;
        using var transform = encrypt ? des.CreateEncryptor() : des.CreateDecryptor();
        return transform.TransformFinalBlock(data, 0, data.Length);
    }

    /// <summary>密码加密：与 CS 的 fnEncrypt 输出一致（小写十六进制）。</summary>
    public static string EncryptPassword(string? plain)
    {
        var text = plain ?? string.Empty;
        if (text.Length == 0) return string.Empty;
        var bytes = Ansi.GetBytes(text);
        var size = (bytes.Length + 7) / 8 * 8;
        var buffer = new byte[size];
        Array.Copy(bytes, buffer, bytes.Length);
        var cipher = DesTransform(buffer, true);
        var sb = new StringBuilder(cipher.Length * 2);
        foreach (var b in cipher) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    /// <summary>密码解密，仅用于「允许查看明文」的受控场景（默认关闭）。</summary>
    public static string DecryptPassword(string? hex)
    {
        var text = (hex ?? string.Empty).Trim();
        if (text.Length == 0 || text.Length % 16 != 0) return string.Empty;
        var buffer = new byte[text.Length / 2];
        for (var i = 0; i < buffer.Length; i++)
        {
            if (!byte.TryParse(text.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                return string.Empty;
            buffer[i] = b;
        }
        var plain = DesTransform(buffer, false);
        var end = plain.Length;
        while (end > 0 && plain[end - 1] == 0) end--;
        return Ansi.GetString(plain, 0, end);
    }

    /// <summary>校验卡密码：数据库存密文，输入为明文，按 CS 的大小写不敏感比较。</summary>
    public static bool PasswordEquals(string? stored, string? input)
    {
        var db = (stored ?? string.Empty).Trim();
        var text = (input ?? string.Empty).Trim();
        if (db.Length == 0 || text.Length == 0) return false;
        return string.Equals(db, EncryptPassword(text), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>卡号加密值（C_PSD）：EncdDecd.EncodeString，两位大写十六进制。</summary>
    public static string EncryptCardNo(string? cardNo)
    {
        var text = (cardNo ?? string.Empty).Trim();
        if (text.Length == 0) return string.Empty;
        var bytes = Ansi.GetBytes(text);
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    /// <summary>卡号解密，用于核对 C_PSD 与 Card_No 是否一致。</summary>
    public static string DecryptCardNo(string? hex)
    {
        var text = (hex ?? string.Empty).Trim();
        if (text.Length == 0 || text.Length % 2 != 0) return string.Empty;
        var bytes = new byte[text.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            if (!byte.TryParse(text.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                return string.Empty;
            bytes[i] = b;
        }
        return Ansi.GetString(bytes);
    }

    /// <summary>卡密码录入规则：1 至 20 位可打印 ASCII（CS 列为 varchar(20)，且避免中文编码歧义）。</summary>
    public static bool IsValidPassword(string? password, bool allowEmpty, out string error)
    {
        error = string.Empty;
        var text = password ?? string.Empty;
        if (text.Length == 0)
        {
            if (allowEmpty) return true;
            error = "卡密码不能为空";
            return false;
        }
        if (text.Length > 20)
        {
            error = "卡密码长度不能超过 20 位";
            return false;
        }
        foreach (var c in text)
        {
            if (c < 32 || c > 126)
            {
                error = "卡密码只能使用可打印的英文字符与数字";
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 超级密码校验，沿用 CS 的 uValidateSuperPwd：固定串 ST_ONECARD，或 Admin 用户密码的明文。
    /// 由配置项 SuperPwd.Enabled 控制是否启用。
    /// </summary>
    public static bool VerifySuperPassword(SqlSession db, string? input, out string error)
    {
        error = string.Empty;
        var text = (input ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            error = "请输入复核码";
            return false;
        }
        if (CardBiz.ConfigInt(db, "SuperPwd.Enabled", 1) != 1)
        {
            error = "超级密码功能已关闭";
            return false;
        }
        if (text == "ST_ONECARD") return true;

        var adminPwd = db.Scalar("SELECT TOP 1 User_Pwd FROM dbo.ST_UserInfo WHERE User_No = 'Admin'") as string;
        if (string.IsNullOrWhiteSpace(adminPwd))
        {
            error = "复核码不正确";
            return false;
        }
        // CS 的 User_Pwd 也是 fnEncrypt 密文，先解密再比对，兼容历史明文数据。
        var plain = DecryptPassword(adminPwd);
        if (string.Equals(plain, text, StringComparison.Ordinal)) return true;
        if (string.Equals(adminPwd.Trim(), text, StringComparison.OrdinalIgnoreCase)) return true;
        error = "复核码不正确";
        return false;
    }
}

/// <summary>卡业务公共工具：参数读取、卡号占用、黑名单、快照、审计与密码失败计数。</summary>
public static class CardBiz
{
    /// <summary>参数读取：优先 CS 的 ST_SysParam（现场已有值），缺失时回退 BS_SysConfig。</summary>
    public static string Config(SqlSession db, string key, string fallback)
    {
        try
        {
            var value = db.Scalar("SELECT TOP 1 Param_Value FROM dbo.ST_SysParam WHERE Param_Code = @k", SqlSession.P("@k", key)) as string;
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }
        catch
        {
            // ST_SysParam 缺失或结构差异时不影响主流程
        }
        return CardLookup.ConfigValue(db, key, fallback);
    }

    public static int ConfigInt(SqlSession db, string key, int fallback)
    {
        var text = Config(db, key, fallback.ToString(CultureInfo.InvariantCulture));
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    /// <summary>金额校验：非负、最多两位小数、上限 99999999。</summary>
    public static bool IsValidMoney(decimal? value, bool allowNull, out string error)
    {
        error = string.Empty;
        if (value == null)
        {
            if (allowNull) return true;
            error = "金额不能为空";
            return false;
        }
        var amount = value.Value;
        if (amount < 0)
        {
            error = "金额不能为负数";
            return false;
        }
        if (amount > 99999999m)
        {
            error = "金额超出允许范围";
            return false;
        }
        if (decimal.Round(amount, 2) != amount)
        {
            error = "金额最多保留两位小数";
            return false;
        }
        return true;
    }

    /// <summary>卡号占用：返回占用该卡号的其他在册人员。</summary>
    public static int CardOwner(SqlSession db, string cardNo, int excludePersonId)
    {
        if (string.IsNullOrWhiteSpace(cardNo)) return 0;
        return db.ScalarInt(
            "SELECT TOP 1 Person_ID FROM dbo.ST_Person WHERE Card_No = @no AND ISNULL(Is_Del, 0) = 0 AND Person_ID <> @id",
            SqlSession.P("@no", cardNo.Trim()), SqlSession.P("@id", excludePersonId));
    }

    public static bool IsCardDisabled(SqlSession db, string cardNo)
    {
        if (string.IsNullOrWhiteSpace(cardNo)) return false;
        return db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_DisableCard WHERE Card_No = @no",
            SqlSession.P("@no", cardNo.Trim())) > 0;
    }

    /// <summary>出纳卡校验：ST_UserInfo.Card_No 已被操作员占用时不能作为人员卡。</summary>
    public static bool IsCashierCard(SqlSession db, string cardNo)
    {
        if (string.IsNullOrWhiteSpace(cardNo)) return false;
        return db.ScalarInt(
            "SELECT COUNT(1) FROM dbo.ST_UserInfo WHERE Card_No = @no AND ISNULL(Is_Del, 0) = 0",
            SqlSession.P("@no", cardNo.Trim())) > 0;
    }

    /// <summary>
    /// 黑名单命中时间。ST_BlackPerson.Del_Sign 在 CS 源码中未被写入，
    /// 因此以记录存在 + 时间窗口（ST_CardInfo 全局参数 Day_DelSysBlack，默认 182 天）判断。
    /// </summary>
    public static DateTime? BlacklistedSince(SqlSession db, string cardNo, int windowDays)
    {
        if (string.IsNullOrWhiteSpace(cardNo)) return null;
        var days = windowDays <= 0 ? 182 : windowDays;
        var value = db.Scalar(
            "SELECT TOP 1 Del_Date FROM dbo.ST_BlackPerson WHERE Card_No = @no AND (Del_Date IS NULL OR Del_Date >= DATEADD(day, -@days, GETDATE())) ORDER BY Del_Date DESC",
            SqlSession.P("@no", cardNo.Trim()), SqlSession.P("@days", days));
        if (value == null) return null;
        return value is DateTime time ? time : (DateTime?)null;
    }

    /// <summary>账户快照：ST_Card + ST_Person + ST_CardType + ST_CardInfo（全局单记录）的一次读取。</summary>
    public static CardSnapshotRow? Snapshot(SqlSession db, int personId)
    {
        return db.QuerySingle<CardSnapshotRow>(@"
SELECT c.Person_ID          AS personId,
       p.Person_No          AS personNo,
       p.Person_Name        AS personName,
       p.Card_No            AS cardNo,
       p.Card_SN            AS cardSn,
       p.Dept_ID            AS deptId,
       d.Dept_No            AS deptNo,
       d.Dept_Name          AS deptName,
       p.Is_Del             AS personIsDel,
       c.Type_No            AS typeNo,
       ct.Type_Name         AS typeName,
       ct.Validity          AS validity,
       ct.Min_Balance       AS minBalance,
       ct.SF_Max_Fund       AS sfMaxFund,
       ct.SF_Max_DayFund    AS sfMaxDayFund,
       ct.SF_LimitTimes     AS sfLimitTimes,
       ct.Max_IcFunds       AS maxIcFunds,
       ct.Small_Discount    AS smallDiscount,
       c.Card_Password      AS cardPassword,
       c.Card_NewDate       AS newDate,
       c.Card_ValidDate     AS validDate,
       c.Card_Foregift      AS foregift,
       c.Card_Charge        AS charge,
       c.Card_Balance       AS balance,
       c.Card_SmallBalance  AS smallBalance,
       c.Subsidy_Fund       AS subsidyFund,
       c.IC_Balance         AS icBalance,
       c.Card_State         AS cardState,
       c.User_No            AS cardUserNo,
       c.Remark             AS cardRemark,
       c.Update_Time        AS updateTime,
       ISNULL(ci.Card_Foregift, 0)    AS infoForegift,
       ISNULL(ci.Card_Charge, 0)      AS infoCharge,
       ISNULL(ci.SmallAccount_Max, 0) AS smallAccountMax,
       ISNULL(ci.Day_DelSysBlack, 182) AS blackDays,
       ci.Subsidy_CutOffDate AS subsidyCutOffDate,
       ext.Lock_Reason_Code AS lockReasonCode,
       ext.Lock_Reason_Text AS lockReasonText,
       ext.Lock_Time        AS lockTime,
       ext.Last_Op_Type     AS lastOpType,
       ext.Last_Op_Time     AS lastOpTime,
       ext.Last_Op_User     AS lastOpUser
  FROM dbo.ST_Card c
  INNER JOIN dbo.ST_Person p ON p.Person_ID = c.Person_ID
  LEFT  JOIN dbo.ST_Department d ON d.Dept_ID = p.Dept_ID
  LEFT  JOIN dbo.ST_CardType ct ON RTRIM(ct.Type_No) = RTRIM(c.Type_No)
  OUTER APPLY (SELECT TOP 1 * FROM dbo.ST_CardInfo ORDER BY Card_Type) ci
  LEFT  JOIN dbo.BS_CardExt ext ON ext.Person_ID = c.Person_ID
 WHERE c.Person_ID = @id", SqlSession.P("@id", personId));
    }

    /// <summary>近期未结算消费：XF_Consume 中未回传（Download_Time 为空）且发生在窗口内的记录。</summary>
    public static int InflightConsume(SqlSession db, int personId, int hours)
    {
        try
        {
            return db.ScalarInt(
                "SELECT COUNT(1) FROM dbo.XF_Consume WHERE Person_ID = @id AND Download_Time IS NULL AND Consume_Time >= DATEADD(hour, -@hours, GETDATE())",
                SqlSession.P("@id", personId), SqlSession.P("@hours", hours));
        }
        catch
        {
            // 部分现场无 XF_Consume 或结构不同，校验降级为通过
            return 0;
        }
    }

    /// <summary>写卡操作审计，返回日志主键。Request_No 有唯一过滤索引，重复提交会抛唯一冲突。</summary>
    public static long WriteLog(SqlSession db, CardLogEntry entry)
    {
        var value = db.Scalar(@"
INSERT INTO dbo.BS_CardLog
    (Person_ID, Card_No, New_Card_No, Op_Type, Before_State, After_State,
     Amount_Balance, Amount_Small, Amount_Foregift, Amount_Charge,
     Refund_Amount, Modify_Fee, Request_No, Source, Op_User, Client_IP, Remark, Ext1, Ext2)
OUTPUT INSERTED.ID_KEY
VALUES
    (@personId, @cardNo, @newCardNo, @opType, @before, @after,
     @balance, @small, @foregift, @charge,
     @refund, @fee, @requestNo, @source, @opUser, @ip, @remark, @ext1, @ext2)",
            SqlSession.P("@personId", entry.PersonId),
            SqlSession.P("@cardNo", Cut(entry.CardNo, 12)),
            SqlSession.P("@newCardNo", Cut(entry.NewCardNo, 12)),
            SqlSession.P("@opType", entry.OpType),
            SqlSession.P("@before", entry.BeforeState),
            SqlSession.P("@after", entry.AfterState),
            SqlSession.P("@balance", entry.Balance),
            SqlSession.P("@small", entry.SmallBalance),
            SqlSession.P("@foregift", entry.Foregift),
            SqlSession.P("@charge", entry.Charge),
            SqlSession.P("@refund", entry.RefundAmount),
            SqlSession.P("@fee", entry.ModifyFee),
            SqlSession.P("@requestNo", Cut(string.IsNullOrWhiteSpace(entry.RequestNo) ? null : entry.RequestNo!.Trim(), 40)),
            SqlSession.P("@source", Cut(entry.Source ?? "WEB", 20)),
            SqlSession.P("@opUser", Cut(entry.OpUser, 20) ?? string.Empty),
            SqlSession.P("@ip", Cut(entry.ClientIp, 50)),
            SqlSession.P("@remark", Cut(entry.Remark, 200)),
            SqlSession.P("@ext1", Cut(entry.Ext1, 100)),
            SqlSession.P("@ext2", Cut(entry.Ext2, 100)));
        return value == null ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    /// <summary>按请求号查历史动作，用于幂等返回首次结果。</summary>
    public static CardLogRow? FindLogByRequest(SqlSession db, string? requestNo)
    {
        if (string.IsNullOrWhiteSpace(requestNo)) return null;
        return db.QuerySingle<CardLogRow>(@"
SELECT ID_KEY        AS idKey,
       Person_ID     AS personId,
       Card_No       AS cardNo,
       New_Card_No   AS newCardNo,
       Op_Type       AS opType,
       Before_State  AS beforeState,
       After_State   AS afterState,
       Amount_Balance AS balance,
       Amount_Small  AS smallBalance,
       Refund_Amount AS refundAmount,
       Modify_Fee    AS modifyFee,
       Op_User       AS opUser,
       Op_Time       AS opTime,
       Remark        AS remark
  FROM dbo.BS_CardLog WHERE Request_No = @no", SqlSession.P("@no", requestNo.Trim()));
    }

    /// <summary>更新扩展表的最近操作与状态原因；扩展表按需插入。</summary>
    public static void TouchExt(
        SqlSession db,
        int personId,
        string? cardNo,
        string opType,
        string opUser,
        int? lockReasonCode,
        string? reasonCode,
        string? reasonText,
        DateTime? lockTime)
    {
        db.Exec(@"
IF EXISTS (SELECT 1 FROM dbo.BS_CardExt WHERE Person_ID = @id)
    UPDATE dbo.BS_CardExt
       SET Card_No = @cardNo, Last_Op_Type = @opType, Last_Op_Time = GETDATE(), Last_Op_User = @user,
           Lock_Reason_Code = @code, Lock_Reason_Text = @text, Lock_Time = @lockTime,
           Pwd_Fail_Count = 0, Pwd_Lock_Until = NULL, Update_Time = GETDATE()
     WHERE Person_ID = @id;
ELSE
    INSERT INTO dbo.BS_CardExt (Person_ID, Card_No, Last_Op_Type, Last_Op_Time, Last_Op_User,
                                Lock_Reason_Code, Lock_Reason_Text, Lock_Time)
    VALUES (@id, @cardNo, @opType, GETDATE(), @user, @code, @text, @lockTime);",
            SqlSession.P("@id", personId),
            SqlSession.P("@cardNo", Cut(cardNo, 12)),
            SqlSession.P("@opType", opType),
            SqlSession.P("@user", Cut(opUser, 20) ?? string.Empty),
            SqlSession.P("@code", Cut(reasonCode, 20)),
            SqlSession.P("@text", Cut(reasonText, 100)),
            SqlSession.P("@lockTime", lockTime));
    }

    /// <summary>卡密码失败计数与锁定（只影响 BS 操作，不写 ST_Card）。</summary>
    public static void PasswordFailed(SqlSession db, int personId, int maxFail, int lockMinutes, out int failCount, out DateTime? lockUntil)
    {
        db.Exec(@"
IF NOT EXISTS (SELECT 1 FROM dbo.BS_CardExt WHERE Person_ID = @id)
    INSERT INTO dbo.BS_CardExt (Person_ID, Pwd_Fail_Count) VALUES (@id, 1);
ELSE
    UPDATE dbo.BS_CardExt SET Pwd_Fail_Count = ISNULL(Pwd_Fail_Count, 0) + 1, Update_Time = GETDATE()
     WHERE Person_ID = @id;", SqlSession.P("@id", personId));

        failCount = db.ScalarInt("SELECT ISNULL(Pwd_Fail_Count, 0) FROM dbo.BS_CardExt WHERE Person_ID = @id",
            SqlSession.P("@id", personId));
        lockUntil = null;
        if (failCount >= maxFail)
        {
            lockUntil = DateTime.Now.AddMinutes(lockMinutes <= 0 ? 10 : lockMinutes);
            db.Exec("UPDATE dbo.BS_CardExt SET Pwd_Fail_Count = 0, Pwd_Lock_Until = @until, Update_Time = GETDATE() WHERE Person_ID = @id",
                SqlSession.P("@until", lockUntil.Value), SqlSession.P("@id", personId));
        }
    }

    public static void PasswordPassed(SqlSession db, int personId)
    {
        db.Exec("UPDATE dbo.BS_CardExt SET Pwd_Fail_Count = 0, Pwd_Lock_Until = NULL, Update_Time = GETDATE() WHERE Person_ID = @id",
            SqlSession.P("@id", personId));
    }

    public static DateTime? PasswordLockedUntil(SqlSession db, int personId)
    {
        var value = db.Scalar("SELECT Pwd_Lock_Until FROM dbo.BS_CardExt WHERE Person_ID = @id", SqlSession.P("@id", personId));
        if (value is DateTime time && time > DateTime.Now) return time;
        return null;
    }

    public static string? Cut(string? value, int length)
    {
        if (value == null) return null;
        var text = value.Trim();
        if (text.Length == 0) return null;
        return text.Length <= length ? text : text.Substring(0, length);
    }

    /// <summary>卡类编号归一：ST_CardType.Type_No 为 3 位定长，统一 Trim 去掉尾随空格后再比较。</summary>
    public static string NormalizeTypeNo(string? value) => (value ?? string.Empty).Trim();
}

/// <summary>卡操作审计实体（写 BS_CardLog）。</summary>
public class CardLogEntry
{
    public int PersonId { get; set; }
    public string? CardNo { get; set; }
    public string? NewCardNo { get; set; }
    public string OpType { get; set; } = string.Empty;
    public int? BeforeState { get; set; }
    public int? AfterState { get; set; }
    public decimal? Balance { get; set; }
    public decimal? SmallBalance { get; set; }
    public decimal? Foregift { get; set; }
    public decimal? Charge { get; set; }
    public decimal? RefundAmount { get; set; }
    public decimal? ModifyFee { get; set; }
    public string? RequestNo { get; set; }
    public string? Source { get; set; }
    public string? OpUser { get; set; }
    public string? ClientIp { get; set; }
    public string? Remark { get; set; }
    public string? Ext1 { get; set; }
    public string? Ext2 { get; set; }
}

/* ---------------- 数据行 ---------------- */

public class CardSnapshotRow
{
    public int personId { get; set; }
    public string? personNo { get; set; }
    public string? personName { get; set; }
    public string? cardNo { get; set; }
    public string? cardSn { get; set; }
    public int? deptId { get; set; }
    public string? deptNo { get; set; }
    public string? deptName { get; set; }
    public bool? personIsDel { get; set; }
    public string? typeNo { get; set; }
    public string? typeName { get; set; }
    public int? validity { get; set; }
    public decimal? minBalance { get; set; }
    public decimal? sfMaxFund { get; set; }
    public decimal? sfMaxDayFund { get; set; }
    public int? sfLimitTimes { get; set; }
    public decimal? maxIcFunds { get; set; }
    public int? smallDiscount { get; set; }
    public string? cardPassword { get; set; }
    public DateTime? newDate { get; set; }
    public DateTime? validDate { get; set; }
    public decimal? foregift { get; set; }
    public decimal? charge { get; set; }
    public decimal? balance { get; set; }
    public decimal? smallBalance { get; set; }
    public decimal? subsidyFund { get; set; }
    public decimal? icBalance { get; set; }
    public int? cardState { get; set; }
    public string? cardUserNo { get; set; }
    public string? cardRemark { get; set; }
    public DateTime? updateTime { get; set; }
    public decimal? infoForegift { get; set; }
    public decimal? infoCharge { get; set; }
    public decimal? smallAccountMax { get; set; }
    public int? blackDays { get; set; }
    public DateTime? subsidyCutOffDate { get; set; }
    public string? lockReasonCode { get; set; }
    public string? lockReasonText { get; set; }
    public DateTime? lockTime { get; set; }
    public string? lastOpType { get; set; }
    public DateTime? lastOpTime { get; set; }
    public string? lastOpUser { get; set; }
}

public class CardLogRow
{
    public long idKey { get; set; }
    public int personId { get; set; }
    public string? cardNo { get; set; }
    public string? newCardNo { get; set; }
    public string? opType { get; set; }
    public int? beforeState { get; set; }
    public int? afterState { get; set; }
    public decimal? balance { get; set; }
    public decimal? smallBalance { get; set; }
    public decimal? refundAmount { get; set; }
    public decimal? modifyFee { get; set; }
    public string? opUser { get; set; }
    public DateTime? opTime { get; set; }
    public string? remark { get; set; }
    public string? ext1 { get; set; }
    public string? ext2 { get; set; }
}
