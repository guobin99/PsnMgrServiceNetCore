using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.Card;

/// <summary>
/// 人员导入导出：模板下载、上传解析、预演、提交、失败清单、重试与导出。
/// 导入三种模式（CREATE/UPDATE/OVERWRITE）与批次追踪写 BS_ImportJob / BS_ImportRow，
/// 主数据仍写 ST_Person，写入顺序与 CS 导入工具一致。
/// </summary>
[Route("api/card/imports")]
[ApiController]
public class CardImportsController : ControllerBase
{
    private static readonly string[] TemplateHeader =
    {
        "人员编号", "姓名", "部门编号", "部门名称", "卡号", "人员类型", "性别",
        "出生日期", "手机", "身份证号", "民族", "职务", "参加工作时间", "备注",
    };

    private static readonly JsonSerializerOptions RowJson = new() { PropertyNameCaseInsensitive = true };

    private readonly AuditService _audit;
    private readonly PermissionService _permission;

    public CardImportsController(AuditService audit, PermissionService permission)
    {
        _audit = audit;
        _permission = permission;
    }

    private string CurrentUser => HttpContext?.User.GetUserNo() ?? string.Empty;

    /// <summary>GET api/card/imports/template：下载导入模板。</summary>
    [HttpGet("template")]
    [RequirePermission("card:import:template")]
    public IActionResult Template()
    {
        var rows = new List<string[]>
        {
            TemplateHeader,
            new[] { "P000001", "示例人员", "1001", "示例部门", "", "01", "男", "1990-01-01", "13800000000", "", "01", "", "2020-01-01", "示例行，导入前请删除" },
        };
        var bytes = CardXlsx.Build("人员导入模板", rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "人员导入模板.xlsx");
    }

    /// <summary>GET api/card/imports：批次列表。</summary>
    [HttpGet]
    [RequirePermission("card:import:view")]
    public IActionResult Jobs([FromQuery] string? jobType = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = CardRules.ClampPage(page);
        pageSize = CardRules.ClampPageSize(pageSize);

        using var db = SqlAccess.Open();
        var where = "1 = 1";
        var parameters = new List<SqlParameter>();
        if (!string.IsNullOrWhiteSpace(jobType))
        {
            where += " AND Job_Type = @type";
            parameters.Add(SqlSession.P("@type", jobType.Trim().ToUpperInvariant()));
        }

        var total = db.ScalarInt("SELECT COUNT(1) FROM dbo.BS_ImportJob WHERE " + where, parameters.ToArray());
        parameters.Add(SqlSession.P("@offset", (page - 1) * pageSize));
        parameters.Add(SqlSession.P("@size", pageSize));

        // 兼容 SQL Server 2008 R2：用 ROW_NUMBER 代替 OFFSET/FETCH。
        var items = db.Query<CardImportJobRow>($@"
SELECT * FROM (
    SELECT q.*, ROW_NUMBER() OVER (ORDER BY q.createTime DESC, q.jobId DESC) AS RowNum
    FROM (
SELECT Job_ID AS jobId, Job_Type AS jobType, File_Name AS fileName, Mode AS mode, Status AS status,
       Total_Rows AS totalRows, Success_Rows AS successRows, Fail_Rows AS failRows, Skip_Rows AS skipRows,
       Create_User AS createUser, Create_Time AS createTime, Finish_Time AS finishTime, Error_File AS errorFile
  FROM dbo.BS_ImportJob WHERE {where}
    ) q
) t
WHERE t.RowNum > @offset AND t.RowNum <= @offset + @size
ORDER BY t.RowNum", parameters.ToArray());

        return Api.Ok(new { total, items });
    }

    /// <summary>POST api/card/imports：上传文件并解析为批次行。</summary>
    [HttpPost]
    [RequirePermission("card:import:import")]
    public async Task<IActionResult> Upload(IFormFile? file, [FromForm] string? mode = "CREATE", [FromForm] bool autoCreateDept = false)
    {
        if (file == null || file.Length == 0) return Api.BadRequest("请选择需要导入的文件");
        if (file.Length > 10 * 1024 * 1024) return Api.Fail(422, "42204", "文件不得超过 10MB");

        var importMode = (mode ?? "CREATE").Trim().ToUpperInvariant();
        if (importMode != "CREATE" && importMode != "UPDATE" && importMode != "OVERWRITE")
            return Api.BadRequest("导入模式只能是 CREATE、UPDATE 或 OVERWRITE");

        byte[] bytes;
        using (var stream = new MemoryStream())
        {
            await file.CopyToAsync(stream);
            bytes = stream.ToArray();
        }

        List<string[]> rows;
        try
        {
            rows = CardXlsx.Read(bytes, file.FileName);
        }
        catch (Exception ex)
        {
            return Api.Fail(422, "42204", "导入文件格式错误：" + ex.Message);
        }
        if (rows.Count < 2) return Api.Fail(422, "42204", "文件没有可导入的数据行");
        if (rows.Count > 50001) return Api.Fail(422, "42204", "单次导入不得超过五万行");

        // 表头校验：姓名列必须存在
        var header = rows[0];
        var nameIndex = Array.FindIndex(header, h => CardRules.Trim(h) == "姓名");
        if (nameIndex < 0)
        {
            return Api.Fail(422, "42204", "表头缺少“姓名”列，请使用系统模板");
        }
        var map = BuildColumnMap(header);

        using var db = SqlAccess.Open();
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var duplicated = db.ScalarInt("SELECT COUNT(1) FROM dbo.BS_ImportJob WHERE File_Hash = @hash AND Status IN (4, 5)",
            SqlSession.P("@hash", hash));
        var warning = duplicated > 0 ? "相同文件此前已成功导入，请确认不是重复导入" : null;

        var jobId = Convert.ToInt64(db.Scalar(@"
INSERT INTO dbo.BS_ImportJob (Job_Type, File_Name, File_Hash, Mode, Status, Create_User, Create_Time, Options_Json)
OUTPUT INSERTED.Job_ID
VALUES ('PERSON', @name, @hash, @mode, 1, @user, GETDATE(), @options)",
            SqlSession.P("@name", Cut(file.FileName, 200)),
            SqlSession.P("@hash", hash),
            SqlSession.P("@mode", importMode),
            SqlSession.P("@user", CurrentUser),
            SqlSession.P("@options", JsonSerializer.Serialize(new { autoCreateDept }))) ?? 0L);

        var rowNo = 1;
        var total = 0;
        foreach (var row in rows.Skip(1))
        {
            rowNo++;
            var data = ToRowData(row, map);
            if (string.IsNullOrWhiteSpace(data.PersonName) && string.IsNullOrWhiteSpace(data.PersonNo) && string.IsNullOrWhiteSpace(data.DeptName))
                continue; // 空行
            if (CardRules.Trim(data.PersonName) == "示例人员") continue;

            db.Exec(@"
INSERT INTO dbo.BS_ImportRow (Job_ID, Row_No, Person_No, Card_No, Person_Name, Action, Status, Raw_Json, Create_Time)
VALUES (@job, @row, @no, @card, @name, NULL, 0, @raw, GETDATE())",
                SqlSession.P("@job", jobId), SqlSession.P("@row", rowNo),
                SqlSession.P("@no", Cut(data.PersonNo, 20)), SqlSession.P("@card", Cut(data.CardNo, 12)),
                SqlSession.P("@name", Cut(data.PersonName, 20)),
                SqlSession.P("@raw", JsonSerializer.Serialize(data)));
            total++;
        }

        db.Exec("UPDATE dbo.BS_ImportJob SET Total_Rows = @total WHERE Job_ID = @id",
            SqlSession.P("@total", total), SqlSession.P("@id", jobId));

        _audit.Log(HttpContext, "card-import", "upload", "import-job", jobId.ToString(CultureInfo.InvariantCulture),
            null, new { file = file.FileName, mode = importMode, total });

        return Api.Ok(new { jobId, total, mode = importMode, warning });
    }

    /// <summary>GET api/card/imports/{id}：批次详情与行状态汇总。</summary>
    [HttpGet("{id:long}")]
    [RequirePermission("card:import:view")]
    public IActionResult Job(long id)
    {
        using var db = SqlAccess.Open();
        var job = db.QuerySingle<CardImportJobRow>(@"
SELECT Job_ID AS jobId, Job_Type AS jobType, File_Name AS fileName, Mode AS mode, Status AS status,
       Total_Rows AS totalRows, Success_Rows AS successRows, Fail_Rows AS failRows, Skip_Rows AS skipRows,
       Create_User AS createUser, Create_Time AS createTime, Finish_Time AS finishTime, Options_Json AS optionsJson
  FROM dbo.BS_ImportJob WHERE Job_ID = @id", SqlSession.P("@id", id));
        if (job == null) return Api.NotFound("批次不存在");

        var summary = db.Query<CardImportSummaryRow>(@"
SELECT ISNULL(Action, 'PENDING') AS action, COUNT(1) AS cnt
  FROM dbo.BS_ImportRow WHERE Job_ID = @id GROUP BY ISNULL(Action, 'PENDING')", SqlSession.P("@id", id));

        return Api.Ok(new { job, summary });
    }

    /// <summary>POST api/card/imports/{id}/preview：预演，逐行校验并给出四类数量。</summary>
    [HttpPost("{id:long}/preview")]
    [RequirePermission("card:import:import")]
    public IActionResult Preview(long id)
    {
        using var db = SqlAccess.Open();
        var job = db.QuerySingle<CardImportJobRow>(
            "SELECT Job_ID AS jobId, Mode AS mode, Status AS status, Options_Json AS optionsJson FROM dbo.BS_ImportJob WHERE Job_ID = @id",
            SqlSession.P("@id", id));
        if (job == null) return Api.NotFound("批次不存在");
        if (job.status >= 3) return Api.StateConflict("批次已提交或正在执行，不能重复预演");

        var mode = string.IsNullOrWhiteSpace(job.mode) ? "CREATE" : job.mode!.ToUpperInvariant();
        var autoCreateDept = (job.optionsJson ?? string.Empty).Contains("\"autoCreateDept\":true", StringComparison.OrdinalIgnoreCase);

        var rows = db.Query<CardImportRow>(@"
SELECT ID_KEY AS idKey, Row_No AS rowNo, Raw_Json AS rawJson FROM dbo.BS_ImportRow WHERE Job_ID = @id ORDER BY Row_No",
            SqlSession.P("@id", id));

        var fileNos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fileCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var createCount = 0;
        var updateCount = 0;
        var skipCount = 0;
        var errorCount = 0;
        var errors = new List<object>();

        db.Begin();
        try
        {
            foreach (var row in rows)
            {
                var data = Deserialize(row.rawJson);
                var result = ValidateRow(db, data, mode, autoCreateDept, fileNos, fileCards, out var action, out var errorCode, out var errorMessage);

                db.Exec("UPDATE dbo.BS_ImportRow SET Action = @action, Status = @status, Error_Code = @code, Error_Message = @message, " +
                        "Person_No = @no, Card_No = @card, Person_Name = @name, Person_ID = @personId WHERE ID_KEY = @key",
                    SqlSession.P("@action", action),
                    SqlSession.P("@status", (byte)(action == "ERROR" ? 2 : 0)),
                    SqlSession.P("@code", errorCode),
                    SqlSession.P("@message", Cut(errorMessage, 500)),
                    SqlSession.P("@no", Cut(data.PersonNo, 20)),
                    SqlSession.P("@card", Cut(data.CardNo, 12)),
                    SqlSession.P("@name", Cut(data.PersonName, 20)),
                    SqlSession.P("@personId", result),
                    SqlSession.P("@key", row.idKey));

                switch (action)
                {
                    case "CREATE": createCount++; break;
                    case "UPDATE": updateCount++; break;
                    case "SKIP": skipCount++; break;
                    default:
                        errorCount++;
                        if (errors.Count < 100)
                            errors.Add(new { row = row.rowNo, column = ColumnOf(errorCode), code = errorCode, message = errorMessage });
                        break;
                }
            }

            db.Exec(@"UPDATE dbo.BS_ImportJob SET Status = 2, Total_Rows = @total, Success_Rows = @success,
                             Fail_Rows = @fail, Skip_Rows = @skip WHERE Job_ID = @id",
                SqlSession.P("@total", rows.Count), SqlSession.P("@success", createCount + updateCount),
                SqlSession.P("@fail", errorCount), SqlSession.P("@skip", skipCount), SqlSession.P("@id", id));
            db.Commit();
        }
        catch (Exception)
        {
            db.Rollback();
            throw;
        }

        return Api.Ok(new
        {
            jobId = id,
            total = rows.Count,
            createCount,
            updateCount,
            skipCount,
            errorCount,
            errors,
        });
    }

    /// <summary>POST api/card/imports/{id}/commit：提交，任一错误行阻断整批。</summary>
    [HttpPost("{id:long}/commit")]
    [RequirePermission("card:import:import")]
    public IActionResult Commit(long id)
    {
        using var db = SqlAccess.Open();
        var job = db.QuerySingle<CardImportJobRow>(
            "SELECT Job_ID AS jobId, Mode AS mode, Status AS status, Options_Json AS optionsJson FROM dbo.BS_ImportJob WHERE Job_ID = @id",
            SqlSession.P("@id", id));
        if (job == null) return Api.NotFound("批次不存在");
        if (job.status < 2) return Api.StateConflict("请先执行预演");
        if (job.status > 2) return Api.StateConflict("批次已提交");

        var errorRows = db.ScalarInt("SELECT COUNT(1) FROM dbo.BS_ImportRow WHERE Job_ID = @id AND Action = 'ERROR'",
            SqlSession.P("@id", id));
        if (errorRows > 0)
            return Api.Fail(422, "42205", $"存在 {errorRows} 行校验不通过，提交被阻断，请修正后重新上传");

        var mode = string.IsNullOrWhiteSpace(job.mode) ? "CREATE" : job.mode!.ToUpperInvariant();
        var autoCreateDept = (job.optionsJson ?? string.Empty).Contains("\"autoCreateDept\":true", StringComparison.OrdinalIgnoreCase);

        var result = ExecuteCommit(db, id, mode, autoCreateDept, onlyFailed: false);

        _audit.Log(HttpContext, "card-import", "commit", "import-job", id.ToString(CultureInfo.InvariantCulture),
            null, new { success = result.success, failed = result.failed, mode });
        return Api.Ok(new { jobId = id, success = result.success, failed = result.failed, status = result.failed == 0 ? 4 : 5 });
    }

    /// <summary>提交主体：正式提交与失败重试共用，整批一个事务，任一行异常只标记该行。</summary>
    private (int success, int failed) ExecuteCommit(SqlSession db, long id, string mode, bool autoCreateDept, bool onlyFailed)
    {
        var overwrite = mode == "OVERWRITE";
        var batchCode = "IMP" + id.ToString(CultureInfo.InvariantCulture);

        var rows = db.Query<CardImportRow>(@"
SELECT ID_KEY AS idKey, Row_No AS rowNo, Raw_Json AS rawJson, Action AS action, Person_ID AS personId
  FROM dbo.BS_ImportRow WHERE Job_ID = @id AND Action IN ('CREATE', 'UPDATE')" + (onlyFailed ? " AND Status = 2" : "") + @"
 ORDER BY Row_No", SqlSession.P("@id", id));
        if (rows.Count == 0) return (0, 0);

        var success = 0;
        var failed = 0;

        db.Begin();
        try
        {
            db.Exec("UPDATE dbo.BS_ImportJob SET Status = 3 WHERE Job_ID = @id", SqlSession.P("@id", id));

            foreach (var row in rows)
            {
                try
                {
                    var data = Deserialize(row.rawJson);
                    var deptId = ResolveDeptId(db, data, autoCreateDept, out _);
                    if (deptId == null)
                    {
                        failed++;
                        db.Exec("UPDATE dbo.BS_ImportRow SET Status = 2, Error_Code = '40005', Error_Message = '部门不存在' WHERE ID_KEY = @key",
                            SqlSession.P("@key", row.idKey));
                        continue;
                    }

                    var personId = row.personId;
                    if (row.action == "CREATE")
                    {
                        string? personNo = CardRules.Trim(data.PersonNo);
                        if (personNo.Length == 0)
                        {
                            personNo = CardPersonNo.Next(db, out _);
                            if (personNo == null) throw new InvalidOperationException("人员编号生成失败");
                        }

                        var newId = CardPersonNo.NextPersonId(db);
                        db.Exec(@"
INSERT INTO dbo.ST_Person
    (Person_ID, Dept_ID, Card_No, Person_No, Person_Name, Person_Type, Cert_Name, Cert_Code, Duty, Title,
     Nationality, Sex, Birthday, Is_Marry, NativePlace, Address, Residence, OnWork_Date, OffWork_Date,
     Is_Del, Remark, update_time, mobile, id_number)
VALUES
    (@personId, @deptId, @cardNo, @personNo, @personName, @personType, NULL, NULL, @duty, NULL,
     @nationality, @sex, @birthday, 0, NULL, NULL, NULL, @onWorkDate, NULL,
     0, @remark, GETDATE(), @mobile, @idNumber)",
                            SqlSession.P("@personId", newId),
                            SqlSession.P("@deptId", deptId.Value),
                            SqlSession.P("@cardNo", Cut(data.CardNo, 12)),
                            SqlSession.P("@personNo", personNo),
                            SqlSession.P("@personName", Cut(data.PersonName, 20)),
                            SqlSession.P("@personType", ResolveDict(db, "BM_PERSONTYPE", data.PersonType)),
                            SqlSession.P("@duty", ResolveDict(db, "BM_DUTY", data.Duty)),
                            SqlSession.P("@nationality", ResolveDict(db, "BM_NATIONALITY", data.Nationality)),
                            SqlSession.P("@sex", Cut(CardRules.NormalizeSex(data.Sex), 2)),
                            SqlSession.P("@birthday", CardRules.ParseDate(data.Birthday)),
                            SqlSession.P("@onWorkDate", CardRules.NormalizeDate(data.OnWorkDate)),
                            SqlSession.P("@remark", Cut(data.Remark, 50)),
                            SqlSession.P("@mobile", Cut(data.Mobile, 30)),
                            SqlSession.P("@idNumber", Cut(data.IdNumber, 30)));

                        db.Exec("INSERT INTO dbo.ST_SubSysRight (Person_ID, System_No) SELECT @id, 'ST' WHERE NOT EXISTS (SELECT 1 FROM dbo.ST_SubSysRight WHERE Person_ID = @id AND System_No = 'ST')",
                            SqlSession.P("@id", newId));
                        db.Exec(@"
IF NOT EXISTS (SELECT 1 FROM dbo.BS_PersonExt WHERE Person_ID = @id)
    INSERT INTO dbo.BS_PersonExt (Person_ID, BS_Source, BS_Import_Batch, BS_Face_Status, BS_Create_User, BS_Create_Time, BS_Update_User, BS_Update_Time)
    VALUES (@id, 'IMPORT', @batch, 0, @user, GETDATE(), @user, GETDATE())
ELSE
    UPDATE dbo.BS_PersonExt SET BS_Import_Batch = @batch, BS_Update_User = @user, BS_Update_Time = GETDATE() WHERE Person_ID = @id",
                            SqlSession.P("@id", newId), SqlSession.P("@batch", batchCode), SqlSession.P("@user", CurrentUser));

                        personId = newId;
                    }
                    else if (personId != null)
                    {
                        var sets = new List<string>();
                        var parameters = new List<SqlParameter> { SqlSession.P("@id", personId.Value) };

                        void Add(string column, string? value, string parameter)
                        {
                            if (!overwrite && string.IsNullOrWhiteSpace(value)) return;
                            sets.Add(column + " = " + parameter);
                            parameters.Add(SqlSession.P(parameter, value ?? string.Empty));
                        }

                        Add("Person_Name", Cut(data.PersonName, 20), "@name");
                        Add("Card_No", Cut(data.CardNo, 12), "@cardNo");
                        Add("Person_Type", ResolveDict(db, "BM_PERSONTYPE", data.PersonType), "@personType");
                        Add("Duty", ResolveDict(db, "BM_DUTY", data.Duty), "@duty");
                        Add("Nationality", ResolveDict(db, "BM_NATIONALITY", data.Nationality), "@nationality");
                        Add("Sex", Cut(CardRules.NormalizeSex(data.Sex), 2), "@sex");
                        Add("mobile", Cut(data.Mobile, 30), "@mobile");
                        Add("id_number", Cut(data.IdNumber, 30), "@idNumber");
                        Add("Remark", Cut(data.Remark, 50), "@remark");
                        if (deptId != null)
                        {
                            sets.Add("Dept_ID = @deptId");
                            parameters.Add(SqlSession.P("@deptId", deptId.Value));
                        }
                        var birthday = CardRules.ParseDate(data.Birthday);
                        if (birthday != null || overwrite)
                        {
                            sets.Add("Birthday = @birthday");
                            parameters.Add(SqlSession.P("@birthday", birthday));
                        }
                        var onWork = CardRules.NormalizeDate(data.OnWorkDate);
                        if (onWork.Length > 0 || overwrite)
                        {
                            sets.Add("OnWork_Date = @onWork");
                            parameters.Add(SqlSession.P("@onWork", onWork));
                        }
                        sets.Add("update_time = GETDATE()");

                        db.Exec("UPDATE dbo.ST_Person SET " + string.Join(", ", sets) + " WHERE Person_ID = @id", parameters.ToArray());
                        db.Exec(@"
IF NOT EXISTS (SELECT 1 FROM dbo.BS_PersonExt WHERE Person_ID = @id)
    INSERT INTO dbo.BS_PersonExt (Person_ID, BS_Source, BS_Import_Batch, BS_Face_Status, BS_Update_User, BS_Update_Time)
    VALUES (@id, 'IMPORT', @batch, 0, @user, GETDATE())
ELSE
    UPDATE dbo.BS_PersonExt SET BS_Import_Batch = @batch, BS_Update_User = @user, BS_Update_Time = GETDATE() WHERE Person_ID = @id",
                            SqlSession.P("@id", personId.Value), SqlSession.P("@batch", batchCode), SqlSession.P("@user", CurrentUser));
                    }

                    success++;
                    db.Exec("UPDATE dbo.BS_ImportRow SET Status = 1, Person_ID = @personId, Error_Code = NULL, Error_Message = NULL WHERE ID_KEY = @key",
                        SqlSession.P("@personId", personId), SqlSession.P("@key", row.idKey));
                }
                catch (Exception ex)
                {
                    failed++;
                    db.Exec("UPDATE dbo.BS_ImportRow SET Status = 2, Error_Code = '50001', Error_Message = @message WHERE ID_KEY = @key",
                        SqlSession.P("@message", Cut(ex.Message, 500)), SqlSession.P("@key", row.idKey));
                }
            }

            db.Exec(@"UPDATE dbo.BS_ImportJob
   SET Success_Rows = (SELECT COUNT(1) FROM dbo.BS_ImportRow WHERE Job_ID = @id AND Status = 1),
       Fail_Rows = (SELECT COUNT(1) FROM dbo.BS_ImportRow WHERE Job_ID = @id AND Status = 2),
       Status = CASE WHEN EXISTS (SELECT 1 FROM dbo.BS_ImportRow WHERE Job_ID = @id AND Status = 2) THEN 5 ELSE 4 END,
       Finish_Time = GETDATE()
 WHERE Job_ID = @id", SqlSession.P("@id", id));
            db.Commit();
        }
        catch (Exception)
        {
            db.Rollback();
            db.Exec("UPDATE dbo.BS_ImportJob SET Status = 6, Finish_Time = GETDATE() WHERE Job_ID = @id", SqlSession.P("@id", id));
            throw;
        }

        if (failed > 0) RefreshDeptTotal(db);
        return (success, failed);
    }

    /// <summary>GET api/card/imports/{id}/errors：失败清单。</summary>
    [HttpGet("{id:long}/errors")]
    [RequirePermission("card:import:view")]
    public IActionResult Errors(long id, [FromQuery] int page = 1, [FromQuery] int pageSize = 200)
    {
        page = CardRules.ClampPage(page);
        pageSize = CardRules.ClampPageSize(pageSize, 200);

        using var db = SqlAccess.Open();
        var total = db.ScalarInt("SELECT COUNT(1) FROM dbo.BS_ImportRow WHERE Job_ID = @id AND (Status = 2 OR Action = 'ERROR')",
            SqlSession.P("@id", id));
        // 兼容 SQL Server 2008 R2：用 ROW_NUMBER 代替 OFFSET/FETCH。
        var items = db.Query<CardImportErrorRow>(@"
SELECT * FROM (
    SELECT q.*, ROW_NUMBER() OVER (ORDER BY q.rowNo) AS RowNum
    FROM (
SELECT Row_No AS rowNo, Person_No AS personNo, Card_No AS cardNo, Person_Name AS personName,
       Action AS action, Status AS status, Error_Code AS errorCode, Error_Message AS errorMessage
  FROM dbo.BS_ImportRow WHERE Job_ID = @id AND (Status = 2 OR Action = 'ERROR')
    ) q
) t
WHERE t.RowNum > @offset AND t.RowNum <= @offset + @size
ORDER BY t.RowNum",
            SqlSession.P("@id", id), SqlSession.P("@offset", (page - 1) * pageSize), SqlSession.P("@size", pageSize));

        return Api.Ok(new { total, items });
    }

    /// <summary>POST api/card/imports/{id}/retry：仅重试失败行。</summary>
    [HttpPost("{id:long}/retry")]
    [RequirePermission("card:import:retry")]
    public IActionResult Retry(long id)
    {
        using var db = SqlAccess.Open();
        var job = db.QuerySingle<CardImportJobRow>(
            "SELECT Job_ID AS jobId, Mode AS mode, Status AS status, Options_Json AS optionsJson FROM dbo.BS_ImportJob WHERE Job_ID = @id",
            SqlSession.P("@id", id));
        if (job == null) return Api.NotFound("批次不存在");
        if (job.status < 2) return Api.StateConflict("请先执行预演");

        var mode = string.IsNullOrWhiteSpace(job.mode) ? "CREATE" : job.mode!.ToUpperInvariant();
        var autoCreateDept = (job.optionsJson ?? string.Empty).Contains("\"autoCreateDept\":true", StringComparison.OrdinalIgnoreCase);

        var rows = db.Query<CardImportRow>(@"
SELECT ID_KEY AS idKey, Row_No AS rowNo, Raw_Json AS rawJson FROM dbo.BS_ImportRow
 WHERE Job_ID = @id AND Status = 2 ORDER BY Row_No", SqlSession.P("@id", id));
        if (rows.Count == 0) return Api.Ok(new { jobId = id, retried = 0, success = 0, failed = 0 });

        var fileNos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fileCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var valid = 0;
        var invalid = 0;

        // 先按当前库状态重新校验失败行，再执行导入
        db.Begin();
        try
        {
            foreach (var row in rows)
            {
                var data = Deserialize(row.rawJson);
                var result = ValidateRow(db, data, mode, autoCreateDept, fileNos, fileCards, out var action, out var errorCode, out var errorMessage);
                if (action == "ERROR")
                {
                    invalid++;
                    db.Exec("UPDATE dbo.BS_ImportRow SET Action = 'ERROR', Status = 2, Error_Code = @code, Error_Message = @message WHERE ID_KEY = @key",
                        SqlSession.P("@code", errorCode), SqlSession.P("@message", Cut(errorMessage, 500)), SqlSession.P("@key", row.idKey));
                    continue;
                }

                valid++;
                db.Exec("UPDATE dbo.BS_ImportRow SET Action = @action, Status = 2, Error_Code = NULL, Error_Message = NULL, Person_ID = @personId WHERE ID_KEY = @key",
                    SqlSession.P("@action", action), SqlSession.P("@personId", result), SqlSession.P("@key", row.idKey));
            }
            db.Commit();
        }
        catch (Exception)
        {
            db.Rollback();
            throw;
        }

        if (valid == 0)
        {
            return Api.Ok(new { jobId = id, retried = rows.Count, success = 0, failed = invalid });
        }

        var commit = ExecuteCommit(db, id, mode, autoCreateDept, onlyFailed: true);

        _audit.Log(HttpContext, "card-import", "retry", "import-job", id.ToString(CultureInfo.InvariantCulture),
            null, new { retried = rows.Count, success = commit.success, failed = commit.failed });
        return Api.Ok(new { jobId = id, retried = rows.Count, success = commit.success, failed = commit.failed });
    }

    /// <summary>GET api/card/imports/export：按筛选条件导出人员，敏感字段按权限脱敏。</summary>
    [HttpGet("export")]
    [RequirePermission("card:import:export")]
    public async Task<IActionResult> Export([FromQuery] string? keyword, [FromQuery] int? deptId,
        [FromQuery] bool includeChildren = true, [FromQuery] string? status = null,
        [FromQuery] bool mask = true)
    {
        using var db = SqlAccess.Open();
        var parameters = new List<SqlParameter>();
        var where = new System.Text.StringBuilder("1 = 1");
        if (string.Equals(status, "deleted", StringComparison.OrdinalIgnoreCase)) where.Append(" AND p.Is_Del = 1");
        else if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase)) where.Append(" AND ISNULL(p.Is_Del, 0) = 0");

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            where.Append(" AND (p.Person_No LIKE @key OR p.Person_Name LIKE @key OR p.Card_No LIKE @key OR p.mobile LIKE @key)");
            parameters.Add(SqlSession.P("@key", "%" + keyword.Trim() + "%"));
        }
        if (deptId.HasValue && deptId.Value > 0)
        {
            if (includeChildren)
            {
                var deptNo = db.Scalar("SELECT Dept_No FROM dbo.ST_Department WHERE Dept_ID = @id", SqlSession.P("@id", deptId.Value));
                var prefix = deptNo == null ? string.Empty : Convert.ToString(deptNo, CultureInfo.InvariantCulture) ?? string.Empty;
                if (prefix.Trim().Length == 0) return Api.BadRequest("选择的部门不存在");
                where.Append(" AND (d.Dept_No = @deptNo OR d.Dept_No LIKE @deptLike)");
                parameters.Add(SqlSession.P("@deptNo", prefix.Trim()));
                parameters.Add(SqlSession.P("@deptLike", prefix.Trim() + "%"));
            }
            else
            {
                where.Append(" AND p.Dept_ID = @deptId");
                parameters.Add(SqlSession.P("@deptId", deptId.Value));
            }
        }

        var rows = db.Query<CardPersonRow>($@"
SELECT p.Person_ID AS personId, p.Person_No AS personNo, p.Person_Name AS personName, p.Dept_ID AS deptId,
       d.Dept_No AS deptNo, d.Dept_Name AS deptName, p.Card_No AS cardNo, p.Person_Type AS personType,
       (SELECT TOP 1 Dict_Value FROM dbo.ST_Dictionary WHERE Table_Name = 'BM_PERSONTYPE' AND Dict_Id = p.Person_Type) AS personTypeName,
       (SELECT TOP 1 Dict_Value FROM dbo.ST_Dictionary WHERE Table_Name = 'BM_DUTY' AND Dict_Id = p.Duty) AS dutyName,
       (SELECT TOP 1 Dict_Value FROM dbo.ST_Dictionary WHERE Table_Name = 'BM_NATIONALITY' AND Dict_Id = p.Nationality) AS nationalityName,
       p.Sex AS sex, p.Birthday AS birthday, p.OnWork_Date AS onWorkDate, p.Remark AS remark,
       p.mobile AS mobile, p.id_number AS idNumber, p.Is_Del AS isDel, p.update_time AS updateTime
  FROM dbo.ST_Person p LEFT JOIN dbo.ST_Department d ON d.Dept_ID = p.Dept_ID
 WHERE {where} ORDER BY d.Dept_No, p.Person_No", parameters.ToArray());

        var canSensitive = await _permission.HasPermissionAsync(CurrentUser, "card:person:sensitive");
        var shouldMask = mask && !canSensitive;

        var table = new List<string[]> { TemplateHeader.Concat(new[] { "状态", "更新时间" }).ToArray() };
        foreach (var row in rows)
        {
            table.Add(new[]
            {
                row.personNo ?? string.Empty,
                row.personName ?? string.Empty,
                row.deptNo ?? string.Empty,
                row.deptName ?? string.Empty,
                row.cardNo ?? string.Empty,
                row.personTypeName ?? row.personType ?? string.Empty,
                row.sex ?? string.Empty,
                row.birthday?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                shouldMask ? CardRules.MaskMobile(row.mobile) ?? string.Empty : row.mobile ?? string.Empty,
                shouldMask ? CardRules.MaskIdNumber(row.idNumber) ?? string.Empty : row.idNumber ?? string.Empty,
                row.nationalityName ?? string.Empty,
                row.dutyName ?? string.Empty,
                row.onWorkDate ?? string.Empty,
                row.remark ?? string.Empty,
                row.isDel ? "已注销" : "在册",
                row.updateTime?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? string.Empty,
            });
        }

        var bytes = CardXlsx.Build("人员数据", table);
        _audit.Log(HttpContext, "card-import", "export", "person", null, null,
            new { total = rows.Count, keyword, deptId, status, mask = shouldMask });
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"人员数据_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }

    /* ---------------- 校验与辅助 ---------------- */

    private static Dictionary<string, int> BuildColumnMap(string[] header)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Length; i++)
        {
            var key = CardRules.Trim(header[i]);
            if (key.Length == 0 || map.ContainsKey(key)) continue;
            map[key] = i;
        }
        return map;
    }

    private static string Cell(string[] row, Dictionary<string, int> map, string column)
    {
        if (!map.TryGetValue(column, out var index)) return string.Empty;
        return index < row.Length ? CardRules.Trim(row[index]) : string.Empty;
    }

    private static CardImportRowData ToRowData(string[] row, Dictionary<string, int> map)
    {
        return new CardImportRowData
        {
            PersonNo = Cell(row, map, "人员编号"),
            PersonName = Cell(row, map, "姓名"),
            DeptNo = Cell(row, map, "部门编号"),
            DeptName = Cell(row, map, "部门名称"),
            CardNo = Cell(row, map, "卡号"),
            PersonType = Cell(row, map, "人员类型"),
            Sex = Cell(row, map, "性别"),
            Birthday = Cell(row, map, "出生日期"),
            Mobile = Cell(row, map, "手机"),
            IdNumber = Cell(row, map, "身份证号"),
            Nationality = Cell(row, map, "民族"),
            Duty = Cell(row, map, "职务"),
            OnWorkDate = Cell(row, map, "参加工作时间"),
            Remark = Cell(row, map, "备注"),
        };
    }

    private static CardImportRowData Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new CardImportRowData();
        try
        {
            return JsonSerializer.Deserialize<CardImportRowData>(json, RowJson) ?? new CardImportRowData();
        }
        catch
        {
            return new CardImportRowData();
        }
    }

    private static string ColumnOf(string? errorCode)
    {
        switch (errorCode)
        {
            case "40004": return "人员编号";
            case "40005": return "部门";
            case "40006": return "卡号";
            case "42204": return "姓名";
            default: return string.Empty;
        }
    }

    /// <summary>单行校验：返回匹配到的人员主键；Action 输出 CREATE/UPDATE/SKIP/ERROR。</summary>
    private static int? ValidateRow(
        SqlSession db,
        CardImportRowData data,
        string mode,
        bool autoCreateDept,
        HashSet<string> fileNos,
        HashSet<string> fileCards,
        out string action,
        out string? errorCode,
        out string? errorMessage)
    {
        action = "ERROR";
        errorCode = null;
        errorMessage = null;

        var name = CardRules.Trim(data.PersonName);
        if (name.Length == 0 || name.Length > 20)
        {
            errorCode = "42204";
            errorMessage = "姓名不能为空且不超过 20 个字符";
            return null;
        }

        var deptNo = CardRules.Trim(data.DeptNo);
        var deptName = CardRules.Trim(data.DeptName);
        if (deptNo.Length == 0 && deptName.Length == 0)
        {
            errorCode = "40005";
            errorMessage = "部门编号与部门名称至少填写一个";
            return null;
        }
        // 预演阶段不建部门，只判断能否匹配；允许自动创建时提交阶段再落库
        var deptId = ResolveDeptId(db, data, false, out var deptError);
        if (deptId == null && !autoCreateDept)
        {
            errorCode = "40005";
            errorMessage = deptError ?? "部门不存在";
            return null;
        }

        var personNo = CardRules.Trim(data.PersonNo);
        if (personNo.Length > 0 && !CardRules.IsValidPersonNo(personNo))
        {
            errorCode = "40004";
            errorMessage = "人员编号需为 2 至 20 位字母或数字";
            return null;
        }
        if (personNo.Length > 0 && fileNos.Contains(personNo))
        {
            errorCode = "42205";
            errorMessage = $"人员编号 {personNo} 在文件内重复";
            return null;
        }

        var cardNo = CardRules.Trim(data.CardNo);
        if (cardNo.Length > 0 && !CardRules.IsValidCardNo(cardNo))
        {
            errorCode = "40006";
            errorMessage = "卡号不得超过 12 位";
            return null;
        }
        if (cardNo.Length > 0 && fileCards.Contains(cardNo))
        {
            errorCode = "42205";
            errorMessage = $"卡号 {cardNo} 在文件内重复";
            return null;
        }

        if (!CardRules.IsValidMobile(data.Mobile))
        {
            errorCode = "42204";
            errorMessage = "手机号格式不正确";
            return null;
        }
        if (!CardRules.IsValidIdNumber(data.IdNumber))
        {
            errorCode = "42204";
            errorMessage = "身份证号需为 15 或 18 位";
            return null;
        }
        if (!string.IsNullOrWhiteSpace(data.Birthday) && CardRules.ParseDate(data.Birthday) == null)
        {
            errorCode = "42204";
            errorMessage = "出生日期格式应为 yyyy-MM-dd";
            return null;
        }

        // 库内匹配：人员编号优先，卡号其次
        int? matched = null;
        string? matchedNo = null;
        if (personNo.Length > 0)
        {
            var row = db.QuerySingle<CardImportMatch>(@"
SELECT TOP 1 Person_ID AS personId, Person_No AS personNo, Card_No AS cardNo
  FROM dbo.ST_Person WHERE Person_No = @no AND ISNULL(Is_Del, 0) = 0", SqlSession.P("@no", personNo));
            if (row != null)
            {
                matched = row.personId;
                matchedNo = row.personNo;
            }
        }
        if (cardNo.Length > 0)
        {
            var byCard = db.QuerySingle<CardImportMatch>(@"
SELECT TOP 1 Person_ID AS personId, Person_No AS personNo, Card_No AS cardNo
  FROM dbo.ST_Person WHERE Card_No = @card AND Card_No <> '' AND ISNULL(Is_Del, 0) = 0", SqlSession.P("@card", cardNo));
            if (byCard != null && matched != null && byCard.personId != matched)
            {
                errorCode = "42205";
                errorMessage = "人员编号与卡号分别命中不同人员，请核对后重试";
                return null;
            }
            if (byCard != null && matched == null)
            {
                matched = byCard.personId;
                matchedNo = byCard.personNo;
            }
        }

        // 编码列：编码或名称都匹配不到时置空并记录警告（不阻断）
        if (personNo.Length > 0) fileNos.Add(personNo);
        if (cardNo.Length > 0) fileCards.Add(cardNo);

        if (mode == "CREATE")
        {
            if (matched != null)
            {
                action = "SKIP";
                return matched;
            }
            action = "CREATE";
            return null;
        }

        action = matched != null ? "UPDATE" : "CREATE";
        return matched;
    }

    /// <summary>部门解析：编号优先、名称其次；允许自动创建时返回空并由提交阶段创建。</summary>
    private static int? ResolveDeptId(SqlSession db, CardImportRowData data, bool autoCreate, out string? error)
    {
        error = null;
        var deptNo = CardRules.Trim(data.DeptNo);
        var deptName = CardRules.Trim(data.DeptName);

        if (deptNo.Length > 0)
        {
            var byNo = db.Scalar("SELECT TOP 1 Dept_ID FROM dbo.ST_Department WHERE Dept_No = @no AND ISNULL(Is_Del, 0) = 0",
                SqlSession.P("@no", deptNo));
            if (byNo != null) return Convert.ToInt32(byNo, CultureInfo.InvariantCulture);
        }

        if (deptName.Length > 0)
        {
            var byName = db.Scalar("SELECT TOP 1 Dept_ID FROM dbo.ST_Department WHERE Dept_Name = @name AND ISNULL(Is_Del, 0) = 0 ORDER BY LEN(Dept_No), Dept_No",
                SqlSession.P("@name", deptName));
            if (byName != null) return Convert.ToInt32(byName, CultureInfo.InvariantCulture);
        }

        if (!autoCreate)
        {
            error = deptNo.Length > 0 ? $"部门编号 {deptNo} 不存在" : $"部门名称 {deptName} 不存在";
            return null;
        }

        // 自动创建：挂在配置的根前缀下，与 CS 导入工具一致
        var rootPrefix = CardLookup.ConfigValue(db, "Dept.RootPrefix", "10");
        var parentExists = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_Department WHERE Dept_No = @no",
            SqlSession.P("@no", rootPrefix)) > 0;
        string? genError;
        var newNo = parentExists ? CardDeptNo.Next(db, rootPrefix, out genError) : CardDeptNo.NextRoot(db, out genError);
        if (newNo == null)
        {
            error = genError ?? "部门编号生成失败";
            return null;
        }

        var newId = CardDeptNo.NextDeptId(db);
        db.Exec(@"
INSERT INTO dbo.ST_Department (Dept_ID, Dept_No, Dept_Name, Dept_Chief, Dept_Total, Dept_Duty, Is_Del, Remark,
                               BS_Parent_ID, BS_Level, BS_Sort_No, BS_Dept_Type, Create_Time)
VALUES (@id, @no, @name, NULL, 0, NULL, 0, N'导入时自动创建', NULL, @level, NULL, NULL, GETDATE())",
            SqlSession.P("@id", newId), SqlSession.P("@no", newNo),
            SqlSession.P("@name", Cut(deptName.Length > 0 ? deptName : deptNo, 30)),
            SqlSession.P("@level", OrgPermissionHelper.GetLevel(newNo)));

        return newId;
    }

    /// <summary>编码列解析：既接受编码值也接受名称，匹配失败返回空串。</summary>
    private static string ResolveDict(SqlSession db, string table, string? value)
    {
        var text = CardRules.Trim(value);
        if (text.Length == 0) return string.Empty;
        return CardLookup.ResolveCode(db, table, text) ?? string.Empty;
    }

    private static void RefreshDeptTotal(SqlSession db)
    {
        db.Exec(@"
UPDATE sd SET Dept_Total = ISNULL(x.Cnt, 0)
  FROM dbo.ST_Department sd
  LEFT JOIN (SELECT Dept_ID, COUNT(1) AS Cnt FROM dbo.ST_Person WHERE ISNULL(Is_Del, 0) = 0 GROUP BY Dept_ID) x
    ON x.Dept_ID = sd.Dept_ID");
    }

    private static string Cut(string? value, int length)
    {
        var text = CardRules.Trim(value);
        return text.Length <= length ? text : text.Substring(0, length);
    }
}

public class CardImportRowData
{
    public string? PersonNo { get; set; }
    public string? PersonName { get; set; }
    public string? DeptNo { get; set; }
    public string? DeptName { get; set; }
    public string? CardNo { get; set; }
    public string? PersonType { get; set; }
    public string? Sex { get; set; }
    public string? Birthday { get; set; }
    public string? Mobile { get; set; }
    public string? IdNumber { get; set; }
    public string? Nationality { get; set; }
    public string? Duty { get; set; }
    public string? OnWorkDate { get; set; }
    public string? Remark { get; set; }
}

public class CardImportJobRow
{
    public long jobId { get; set; }
    public string? jobType { get; set; }
    public string? fileName { get; set; }
    public string? mode { get; set; }
    public byte status { get; set; }
    public int totalRows { get; set; }
    public int successRows { get; set; }
    public int failRows { get; set; }
    public int skipRows { get; set; }
    public string? createUser { get; set; }
    public DateTime? createTime { get; set; }
    public DateTime? finishTime { get; set; }
    public string? errorFile { get; set; }
    public string? optionsJson { get; set; }
}

public class CardImportRow
{
    public long idKey { get; set; }
    public int rowNo { get; set; }
    public string? rawJson { get; set; }
    public string? action { get; set; }
    public int? personId { get; set; }
}

public class CardImportSummaryRow
{
    public string? action { get; set; }
    public int cnt { get; set; }
}

public class CardImportErrorRow
{
    public int rowNo { get; set; }
    public string? personNo { get; set; }
    public string? cardNo { get; set; }
    public string? personName { get; set; }
    public string? action { get; set; }
    public byte status { get; set; }
    public string? errorCode { get; set; }
    public string? errorMessage { get; set; }
}

public class CardImportMatch
{
    public int personId { get; set; }
    public string? personNo { get; set; }
    public string? cardNo { get; set; }
}
