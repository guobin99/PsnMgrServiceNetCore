using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.Card;

/// <summary>
/// 导入授权 ID 卡：把卡号白名单写入 ST_EnCard（ID_KEY、Card_No varchar(40)、Remark varchar(10)）。
/// 完全兼容 CS 的 uCardImpExp 文件规则：文件名前四位为 CARD、首行为授权密钥、密钥写 ST_SysParam(ST.ID.Import)。
/// ST_EnCard 无唯一约束，重复卡号在 BS 层逐行查重；批次追溯放 BS_ImportJob / BS_ImportRow。
/// </summary>
[Route("api/card/idcards")]
[ApiController]
public class CardIdCardsController : ControllerBase
{
    private readonly AuditService _audit;

    public CardIdCardsController(AuditService audit)
    {
        _audit = audit;
    }

    private const string KeyParamCode = "ST.ID.Import";
    private const long MaxFileBytes = 10 * 1024 * 1024;

    private string CurrentUser => HttpContext?.User.GetUserNo() ?? string.Empty;
    private string ClientIp => HttpContext?.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

    /// <summary>POST api/card/idcards/parse：解析文件并预览，结果落 BS_ImportJob/Row，返回 parseToken。</summary>
    [HttpPost("parse")]
    [RequirePermission("card:cardImport:import")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Parse([FromForm] IFormFile? file)
    {
        if (file == null || file.Length == 0) return Api.Fail(422, "42206", "请选择要导入的授权卡文件");
        if (file.Length > MaxFileBytes) return Api.Fail(422, "42206", "文件大小不能超过 10MB");

        var fileName = file.FileName ?? string.Empty;
        if (!IsCardFileName(fileName))
            return Api.Fail(422, "42206", "文件名前四位必须为 CARD（不区分大小写），例如 CARD001.txt");

        byte[] bytes;
        using (var stream = new MemoryStream())
        {
            await file.CopyToAsync(stream);
            bytes = stream.ToArray();
        }

        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var text = ReadText(bytes);
        var lines = SplitLines(text);
        if (lines.Count == 0) return Api.Fail(422, "42206", "文件内容为空");

        var key = lines[0];
        if (key.Length == 0) return Api.Fail(422, "42206", "第 1 行必须为授权密钥");

        var dataLines = lines.Skip(1).ToList();
        if (dataLines.Count == 0) return Api.Fail(422, "42206", "第 1 行之后没有可导入的卡号");

        using var db = SqlAccess.Open();

        var existingKey = (db.Scalar("SELECT TOP 1 Param_Value FROM dbo.ST_SysParam WHERE Param_Code = @code",
            SqlSession.P("@code", KeyParamCode)) as string ?? string.Empty).Trim();
        var keyState = existingKey.Length == 0 ? "NEW"
            : string.Equals(existingKey, key, StringComparison.Ordinal) ? "MATCH" : "MISMATCH";

        var duplicateJob = db.QuerySingle<CardIdJobRow>(@"
SELECT TOP 1 Job_ID AS jobId, Create_Time AS createTime, Create_User AS createUser
  FROM dbo.BS_ImportJob
 WHERE Job_Type = 'IDCARD' AND File_Hash = @hash AND Create_Time >= DATEADD(hour, -24, GETDATE())
 ORDER BY Job_ID DESC", SqlSession.P("@hash", hash));

        var userNo = CurrentUser;
        long jobId = Convert.ToInt64(db.Scalar(@"
INSERT INTO dbo.BS_ImportJob (Job_Type, File_Name, File_Hash, Mode, Status, Total_Rows, Create_User, Options_Json)
OUTPUT INSERTED.Job_ID
VALUES ('IDCARD', @name, @hash, @mode, 0, @total, @user, @options)",
            SqlSession.P("@name", CardBiz.Cut(fileName, 200)),
            SqlSession.P("@hash", hash),
            SqlSession.P("@mode", keyState),
            SqlSession.P("@total", dataLines.Count),
            SqlSession.P("@user", userNo),
            SqlSession.P("@options", BuildOptions(key, existingKey.Length == 0))) ?? 0L, CultureInfo.InvariantCulture);

        var shortCode = BuildShortCode(jobId);
        var preview = new List<CardImportPreviewRow>();
        var duplicates = 0;
        var errors = 0;
        var rowNo = 1;

        foreach (var raw in dataLines)
        {
            rowNo++;
            var cardNo = raw.Trim();
            if (cardNo.Length == 0) continue;

            var lengthError = cardNo.Length > 40 ? "卡号长度不能超过 40 位" : null;
            var exists = lengthError == null && db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_EnCard WHERE Card_No = @no",
                SqlSession.P("@no", cardNo)) > 0;

            var status = lengthError != null ? 4 : exists ? 3 : 0;
            if (stateIsDuplicate(status)) duplicates++;
            if (status == 4) errors++;

            db.Exec(@"INSERT INTO dbo.BS_ImportRow (Job_ID, Row_No, Card_No, Action, Status, Error_Code, Error_Message)
                      VALUES (@job, @row, @cardNo, 'IMPORT', @status, @code, @message)",
                SqlSession.P("@job", jobId),
                SqlSession.P("@row", rowNo),
                SqlSession.P("@cardNo", cardNo.Length > 40 ? cardNo.Substring(0, 40) : cardNo),
                SqlSession.P("@status", status),
                SqlSession.P("@code", status == 4 ? "42206" : null),
                SqlSession.P("@message", lengthError ?? (exists ? "卡号已存在于授权表，导入时跳过" : null)));

            if (preview.Count < 20)
            {
                preview.Add(new CardImportPreviewRow
                {
                    rowNo = rowNo, 
                    cardNo=cardNo,
                    status=status,
                    statusText = status == 0 ? "待导入" : status == 3 ? "重复" : "格式错误",
                    message = lengthError ?? (exists ? "已存在，导入时跳过" : null),
                });
            }
        }

        db.Exec("UPDATE dbo.BS_ImportJob SET Skip_Rows = @skip, Fail_Rows = @fail WHERE Job_ID = @id",
            SqlSession.P("@skip", duplicates), SqlSession.P("@fail", errors), SqlSession.P("@id", jobId));

        _audit.LogAndSave(HttpContext, "card-idcard", "parse", "idcardFile", fileName,
            null, new { jobId, total = dataLines.Count, duplicates, errors, keyState });

        return Api.Ok(new
        {
            parseToken = jobId,
            jobId,
            fileName,
            fileHash = hash,
            totalRows = dataLines.Count,
            duplicateRows = duplicates,
            errorRows = errors,
            keyState,
            keyHint = keyState == "MISMATCH" ? "文件首行密钥与系统已保存的密钥不一致，该文件将被整份跳过" : null,
            duplicateFile = duplicateJob == null ? null : new { duplicateJob.jobId, duplicateJob.createTime, duplicateJob.createUser },
            requireDuplicateConfirm = duplicateJob != null,
            shortCode,
            preview,
        }, keyState == "MISMATCH" ? "解析完成，但密钥不匹配" : "解析完成，请确认后执行导入");
    }

    /// <summary>POST api/card/idcards/import：按 parseToken 执行导入（整批一个事务）。</summary>
    [HttpPost("import")]
    [RequirePermission("card:cardImport:import")]
    public IActionResult Import([FromBody] CardIdImportRequest request)
    {
        var jobId = request.ParseToken;
        if (jobId <= 0) return Api.Fail(422, "42206", "请先解析文件");

        using var db = SqlAccess.Open();
        var job = db.QuerySingle<CardIdJobRow>(@"
SELECT Job_ID AS jobId, File_Name AS fileName, File_Hash AS fileHash, Mode AS keyState,
       Status AS status, Total_Rows AS totalRows, Success_Rows AS successRows,
       Fail_Rows AS failRows, Skip_Rows AS skipRows, Create_Time AS createTime, Create_User AS createUser
  FROM dbo.BS_ImportJob WHERE Job_ID = @id AND Job_Type = 'IDCARD'", SqlSession.P("@id", jobId));
        if (job == null) return Api.Fail(422, "42206", "解析结果不存在或已失效，请重新上传");
        if (job.status != 0) return Api.Fail(422, "42206", "该批次已处理，请重新上传文件");

        if (CardBiz.Cut(job.keyState, 20) == "MISMATCH")
        {
            db.Exec("UPDATE dbo.BS_ImportJob SET Status = 3, Finish_Time = GETDATE() WHERE Job_ID = @id", SqlSession.P("@id", jobId));
            return Api.Fail(422, "42206", "文件首行密钥与系统密钥不一致，整份文件已跳过");
        }

        if (request.RequireDuplicateConfirm && job.fileHash != null)
        {
            var duplicated = db.ScalarInt(
                "SELECT COUNT(1) FROM dbo.BS_ImportJob WHERE Job_Type = 'IDCARD' AND File_Hash = @hash AND Job_ID <> @id AND Create_Time >= DATEADD(hour, -24, GETDATE())",
                SqlSession.P("@hash", job.fileHash), SqlSession.P("@id", jobId));
            if (duplicated > 0 && !request.ConfirmDuplicate)
                return Api.Fail(409, "40913", "该文件在 24 小时内已导入过，请勾选「确认重复导入」后再执行");
        }

        var rows = db.Query<CardImportRowItem>(@"
SELECT ID_KEY AS idKey, Row_No AS rowNo, Card_No AS cardNo, Status AS status
  FROM dbo.BS_ImportRow WHERE Job_ID = @id AND Status = 0 ORDER BY Row_No", SqlSession.P("@id", jobId));

        var shortCode = BuildShortCode(jobId);
        var userNo = CurrentUser;
        var success = 0;
        var skipped = 0;
        var failed = 0;

        db.Begin();
        try
        {
            // 密钥不存在时写入，与 CS 行为一致（首次导入以文件首行作为密钥）
            if (CardBiz.Cut(job.keyState, 20) == "NEW")
            {
                var keyLine = ReadKeyLine(db, jobId);
                if (!string.IsNullOrEmpty(keyLine))
                {
                    db.Exec(@"IF NOT EXISTS (SELECT 1 FROM dbo.ST_SysParam WHERE Param_Code = @code)
                                  INSERT INTO dbo.ST_SysParam (Param_Code, Param_Name, Param_Value)
                                  VALUES (@code, 'ID卡导入密钥', @value)",
                        SqlSession.P("@code", KeyParamCode), SqlSession.P("@value", keyLine));
                }
            }

            foreach (var row in rows)
            {
                var cardNo = (row.cardNo ?? string.Empty).Trim();
                if (cardNo.Length == 0) continue;

                // 导入时再次查重，防止解析后其他操作员已写入同卡号
                if (db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_EnCard WHERE Card_No = @no", SqlSession.P("@no", cardNo)) > 0)
                {
                    skipped++;
                    db.Exec("UPDATE dbo.BS_ImportRow SET Status = 3, Error_Message = '卡号已存在，已跳过' WHERE ID_KEY = @id",
                        SqlSession.P("@id", row.idKey));
                    continue;
                }

                db.Exec("INSERT INTO dbo.ST_EnCard (Card_No, Remark) VALUES (@no, @remark)",
                    SqlSession.P("@no", cardNo), SqlSession.P("@remark", shortCode));
                db.Exec("UPDATE dbo.BS_ImportRow SET Status = 1 WHERE ID_KEY = @id", SqlSession.P("@id", row.idKey));
                success++;
            }

            failed = db.ScalarInt("SELECT COUNT(1) FROM dbo.BS_ImportRow WHERE Job_ID = @id AND Status = 4", SqlSession.P("@id", jobId));
            // 密钥原文用完即清，避免长期驻留在批次表
            db.Exec(@"UPDATE dbo.BS_ImportJob SET Options_Json = @options WHERE Job_ID = @id",
                SqlSession.P("@options", "{\"keyCleared\":true}"), SqlSession.P("@id", jobId));
            db.Exec(@"UPDATE dbo.BS_ImportJob
                         SET Status = 2, Success_Rows = @success, Skip_Rows = @skip, Fail_Rows = @fail, Finish_Time = GETDATE()
                       WHERE Job_ID = @id",
                SqlSession.P("@success", success), SqlSession.P("@skip", skipped + job.skipRows),
                SqlSession.P("@fail", failed), SqlSession.P("@id", jobId));

            CardBiz.WriteLog(db, new CardLogEntry
            {
                PersonId = 0,
                OpType = "IDCARD_IMPORT",
                Source = "BATCH",
                OpUser = userNo,
                ClientIp = ClientIp,
                Remark = CardBiz.Cut($"导入授权卡：成功 {success} 条，重复 {skipped} 条", 200),
                Ext1 = CardBiz.Cut(job.fileName, 100),
                Ext2 = shortCode,
            });
            db.Commit();
        }
        catch (Exception ex)
        {
            db.Rollback();
            _audit.LogAndSave(HttpContext, "card-idcard", "import", "idcardFile", job.fileName, null, request, false, ex.Message);
            return Api.ServerError("导入失败，已回滚整批：" + ex.Message);
        }

        _audit.LogAndSave(HttpContext, "card-idcard", "import", "idcardFile", job.fileName,
            null, new { jobId, success, skipped, failed });
        return Api.Ok(new { jobId, success, duplicate = skipped, failed, total = job.totalRows, shortCode },
            $"导入完成：成功 {success} 条，重复 {skipped} 条，失败 {failed} 条");
    }

    /// <summary>GET api/card/idcards：授权卡列表（按卡号反查批次）。</summary>
    [HttpGet]
    [RequirePermission("card:cardImport:view")]
    public IActionResult List(
        [FromQuery] string? keyword,
        [FromQuery] string? batch = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = CardRules.ClampPage(page);
        pageSize = CardRules.ClampPageSize(pageSize, 20);
        if (pageSize > 100) pageSize = 100;

        using var db = SqlAccess.Open();
        var parameters = new List<SqlParameter>();
        var where = new List<string> { "1 = 1" };
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            where.Add("e.Card_No LIKE @kw");
            parameters.Add(SqlSession.P("@kw", "%" + keyword.Trim() + "%"));
        }
        if (!string.IsNullOrWhiteSpace(batch))
        {
            where.Add("e.Remark = @batch");
            parameters.Add(SqlSession.P("@batch", batch.Trim()));
        }
        var filter = string.Join(" AND ", where);

        var total = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_EnCard e WHERE " + filter, parameters.ToArray());
        parameters.Add(SqlSession.P("@offset", (page - 1) * pageSize));
        parameters.Add(SqlSession.P("@size", pageSize));

        var items = db.Query<CardEnCardRow>(@"
SELECT * FROM (
    SELECT e.ID_KEY AS idKey, e.Card_No AS cardNo, e.Remark AS batchCode,
           (SELECT TOP 1 r.Job_ID FROM dbo.BS_ImportRow r WHERE r.Card_No = e.Card_No AND r.Status = 1 ORDER BY r.Job_ID DESC) AS jobId,
           (SELECT COUNT(1) FROM dbo.ST_Person p WHERE p.Card_No = e.Card_No) AS usedCount,
           ROW_NUMBER() OVER (ORDER BY e.ID_KEY DESC) AS RowNum
      FROM dbo.ST_EnCard e
     WHERE " + filter + @"
) t WHERE t.RowNum > @offset AND t.RowNum <= @offset + @size ORDER BY t.RowNum", parameters.ToArray());

        return Api.Ok(new { total, items });
    }

    /// <summary>GET api/card/idcards/import-jobs：导入批次。</summary>
    [HttpGet("import-jobs")]
    [RequirePermission("card:cardImport:view")]
    public IActionResult ImportJobs([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = CardRules.ClampPage(page);
        pageSize = CardRules.ClampPageSize(pageSize, 20);
        if (pageSize > 100) pageSize = 100;

        using var db = SqlAccess.Open();
        var total = db.ScalarInt("SELECT COUNT(1) FROM dbo.BS_ImportJob WHERE Job_Type = 'IDCARD'");
        var items = db.Query<CardIdJobRow>(@"
SELECT * FROM (
    SELECT Job_ID AS jobId, File_Name AS fileName, File_Hash AS fileHash, Mode AS keyState, Status AS status,
           Total_Rows AS totalRows, Success_Rows AS successRows, Fail_Rows AS failRows, Skip_Rows AS skipRows,
           Create_User AS createUser, Create_Time AS createTime, Finish_Time AS finishTime,
           ROW_NUMBER() OVER (ORDER BY Job_ID DESC) AS RowNum
      FROM dbo.BS_ImportJob WHERE Job_Type = 'IDCARD'
) t WHERE t.RowNum > @offset AND t.RowNum <= @offset + @size ORDER BY t.RowNum",
            SqlSession.P("@offset", (page - 1) * pageSize), SqlSession.P("@size", pageSize));

        foreach (var item in items)
        {
            item.statusText = item.status switch
            {
                0 => "待导入",
                2 => "已完成",
                3 => "密钥不匹配",
                _ => "处理中",
            };
            item.keyStateText = CardBiz.Cut(item.keyState, 20) switch
            {
                "NEW" => "首次导入（已写入密钥）",
                "MATCH" => "密钥匹配",
                "MISMATCH" => "密钥不匹配",
                _ => "未校验",
            };
        }
        return Api.Ok(new { total, items });
    }

    /// <summary>GET api/card/idcards/duplicates：重复卡号检测（历史数据治理）。</summary>
    [HttpGet("duplicates")]
    [RequirePermission("card:cardImport:view")]
    public IActionResult Duplicates([FromQuery] int limit = 200)
    {
        if (limit is < 1 or > 1000) limit = 200;
        using var db = SqlAccess.Open();
        var items = db.Query<CardDuplicateRow>(@"
SELECT TOP (@limit) e.Card_No AS cardNo, COUNT(1) AS duplicateCount, MIN(e.ID_KEY) AS minIdKey
  FROM dbo.ST_EnCard e
 GROUP BY e.Card_No
HAVING COUNT(1) > 1
 ORDER BY COUNT(1) DESC, e.Card_No", SqlSession.P("@limit", limit));
        return Api.Ok(new { total = items.Count, items });
    }

    /// <summary>DELETE api/card/idcards/{idKey}：删除授权卡（已被人员使用的卡号禁止删除）。</summary>
    [HttpDelete("{idKey:int}")]
    [RequirePermission("card:cardImport:delete")]
    public IActionResult Delete(int idKey, [FromQuery] string? reason)
    {
        using var db = SqlAccess.Open();
        var row = db.QuerySingle<CardEnCardRow>("SELECT ID_KEY AS idKey, Card_No AS cardNo FROM dbo.ST_EnCard WHERE ID_KEY = @id",
            SqlSession.P("@id", idKey));
        if (row == null) return Api.NotFound("授权卡不存在");

        var used = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_Person WHERE Card_No = @no", SqlSession.P("@no", row.cardNo));
        if (used > 0) return Api.Fail(409, "40910", "该卡号已被人员使用，不允许删除授权");

        var affected = db.Exec("DELETE FROM dbo.ST_EnCard WHERE ID_KEY = @id", SqlSession.P("@id", idKey));
        if (affected == 0) return Api.NotFound("授权卡不存在");

        db.Exec(@"INSERT INTO dbo.BS_CardLog (Person_ID, Card_No, Op_Type, Source, Op_User, Client_IP, Remark)
                  VALUES (0, @cardNo, 'IDCARD_DEL', 'WEB', @user, @ip, @remark)",
            SqlSession.P("@cardNo", CardBiz.Cut(row.cardNo, 12)),
            SqlSession.P("@user", CurrentUser),
            SqlSession.P("@ip", CardBiz.Cut(ClientIp, 50)),
            SqlSession.P("@remark", CardBiz.Cut(string.IsNullOrWhiteSpace(reason) ? "删除授权卡" : reason!.Trim(), 200)));

        _audit.LogAndSave(HttpContext, "card-idcard", "delete", "idcard", row.cardNo, new { row.cardNo }, null);
        return Api.Ok(new { idKey, cardNo = row.cardNo }, "授权卡已删除");
    }

    /* ---------------- 内部方法 ---------------- */

    private static bool IsCardFileName(string fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty);
        if (name.Length < 4) return false;
        return string.Equals(name.Substring(0, 4), "CARD", StringComparison.OrdinalIgnoreCase);
    }

    private static bool stateIsDuplicate(int status) => status == 3;

    private static string ReadText(byte[] bytes)
    {
        try
        {
            var strict = new UTF8Encoding(false, true);
            return strict.GetString(bytes);
        }
        catch
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                return Encoding.GetEncoding(936).GetString(bytes);
            }
            catch
            {
                return Encoding.Latin1.GetString(bytes);
            }
        }
    }

    private static List<string> SplitLines(string text)
    {
        return (text ?? string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.Trim().Trim('\uFEFF'))
            .ToList();
    }

    private static string BuildShortCode(long jobId)
    {
        var text = jobId.ToString(CultureInfo.InvariantCulture);
        return text.Length <= 6 ? text : text.Substring(text.Length - 6);
    }

    /// <summary>
    /// 解析阶段把文件首行密钥暂存在 Options_Json，导入成功后清空。
    /// 仅首次导入（密钥尚不存在）时需要把密钥写入 ST_SysParam，其余情况只用于比对。
    /// </summary>
    private static string BuildOptions(string key, bool keyMissing)
    {
        return System.Text.Json.JsonSerializer.Serialize(new { keyLength = key.Length, key, keyMissing });
    }

    private static string? ReadKeyLine(SqlSession db, long jobId)
    {
        var value = db.Scalar("SELECT Options_Json FROM dbo.BS_ImportJob WHERE Job_ID = @id", SqlSession.P("@id", jobId)) as string;
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(value);
            return doc.RootElement.TryGetProperty("key", out var key) ? key.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}

/* ---------------- 行与请求 ---------------- */

public class CardEnCardRow
{
    public int idKey { get; set; }
    public string? cardNo { get; set; }
    public string? batchCode { get; set; }
    public long? jobId { get; set; }
    public int usedCount { get; set; }
}

public class CardDuplicateRow
{
    public string? cardNo { get; set; }
    public int duplicateCount { get; set; }
    public int minIdKey { get; set; }
}

public class CardIdJobRow
{
    public long jobId { get; set; }
    public string? fileName { get; set; }
    public string? fileHash { get; set; }
    public string? keyState { get; set; }
    public int status { get; set; }
    public string? statusText { get; set; }
    public string? keyStateText { get; set; }
    public int totalRows { get; set; }
    public int successRows { get; set; }
    public int failRows { get; set; }
    public int skipRows { get; set; }
    public string? createUser { get; set; }
    public DateTime? createTime { get; set; }
    public DateTime? finishTime { get; set; }
}

public class CardImportRowItem
{
    public long idKey { get; set; }
    public int rowNo { get; set; }
    public string? cardNo { get; set; }
    public int status { get; set; }
}

public class CardImportPreviewRow
{
    public int rowNo { get; set; }
    public string? cardNo { get; set; }
    public int status { get; set; }
    public string? statusText { get; set; }
    public string? message { get; set; }
}

public class CardIdImportRequest
{
    public long ParseToken { get; set; }
    public bool ConfirmDuplicate { get; set; }
    public bool RequireDuplicateConfirm { get; set; }
}
