using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using PsnMgrService.Models;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.SystemAdmin;

/// <summary>
/// 黑名单管理。权威表仍是 ST_BlackPerson；BS 只补人员关联、卡状态同步与审计列，
/// 释放采用物理删除（与 CS 一致），历史通过 BS_OperLog 追溯。
/// </summary>
[Route("api/system/blacklist")]
[ApiController]
public class BlacklistController : ControllerBase
{
    private const int DefaultRetainDays = 182;

    /// <summary>ST_CardInfo 全表只允许一条记录；独立取 TOP 1，不与其它表按字段关联。</summary>
    private const string RetainDaysExpr = "(SELECT TOP 1 ISNULL(Day_DelSysBlack, 182) FROM dbo.ST_CardInfo)";

    private const string BaseSelect = @"
        SELECT b.ID_KEY AS id, b.Person_ID AS personId, b.Card_No AS cardNo,
               p.Person_No AS personNo, p.Person_Name AS personName, d.Dept_Name AS deptName,
               b.Del_Date AS delDate, b.Del_Sign AS delSign, b.Remark AS remark,
               CAST(c.Card_State AS VARCHAR(4)) AS cardState,
               " + RetainDaysExpr + @" AS retainDays,
               CASE WHEN b.Del_Sign = 1 THEN DATEADD(day, 0, b.Del_Date)
                    ELSE DATEADD(day, " + RetainDaysExpr + @", b.Del_Date) END AS releaseableTime,
               CASE WHEN b.Del_Sign = 1 THEN 1
                    WHEN GETDATE() >= DATEADD(day, " + RetainDaysExpr + @", b.Del_Date) THEN 1
                    ELSE 0 END AS releaseable,
               b.BS_User_No AS addUser,
               CASE WHEN b.Del_Sign = 1 THEN 'system' WHEN b.BS_User_No IS NULL THEN 'cs' ELSE 'manual' END AS source
          FROM dbo.ST_BlackPerson b
          LEFT JOIN dbo.ST_Person p ON p.Person_ID = b.Person_ID
          LEFT JOIN dbo.ST_Department d ON d.Dept_ID = p.Dept_ID
          LEFT JOIN dbo.ST_Card c ON c.Person_ID = b.Person_ID";

    /// <summary>GET api/system/blacklist</summary>
    [HttpGet]
    [RequirePermission("system:blacklist:view")]
    public IActionResult List(
        [FromQuery] string? cardNo, [FromQuery] string? personNo, [FromQuery] string? personName,
        [FromQuery] int? deptId, [FromQuery] DateTime? begin, [FromQuery] DateTime? end,
        [FromQuery] string? source, [FromQuery] bool? releaseable,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100);

        using var db = SqlAccess.Open();
        var sql = BaseSelect + " WHERE 1 = 1";
        var parameters = new List<Microsoft.Data.SqlClient.SqlParameter>();
        if (!string.IsNullOrWhiteSpace(cardNo))
        {
            sql += " AND b.Card_No LIKE @card";
            parameters.Add(SqlSession.P("@card", "%" + cardNo.Trim() + "%"));
        }
        if (!string.IsNullOrWhiteSpace(personNo))
        {
            sql += " AND p.Person_No = @personNo";
            parameters.Add(SqlSession.P("@personNo", personNo.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(personName))
        {
            sql += " AND p.Person_Name LIKE @name";
            parameters.Add(SqlSession.P("@name", "%" + personName.Trim() + "%"));
        }
        if (deptId is > 0)
        {
            sql += @" AND p.Dept_ID IN (SELECT Dept_ID FROM dbo.ST_Department
                                         WHERE Dept_No = (SELECT Dept_No FROM dbo.ST_Department WHERE Dept_ID = @dept)
                                            OR Dept_No LIKE (SELECT Dept_No FROM dbo.ST_Department WHERE Dept_ID = @dept) + '%')";
            parameters.Add(SqlSession.P("@dept", deptId.Value));
        }
        if (begin.HasValue)
        {
            sql += " AND b.Del_Date >= @begin";
            parameters.Add(SqlSession.P("@begin", begin.Value));
        }
        if (end.HasValue)
        {
            sql += " AND b.Del_Date < @end";
            parameters.Add(SqlSession.P("@end", end.Value.Date.AddDays(1)));
        }
        if (!string.IsNullOrWhiteSpace(source))
        {
            switch (source.Trim().ToLowerInvariant())
            {
                case "system":
                    sql += " AND b.Del_Sign = 1";
                    break;
                case "manual":
                    sql += " AND b.Del_Sign = 0 AND b.BS_User_No IS NOT NULL";
                    break;
                case "cs":
                    sql += " AND b.Del_Sign = 0 AND b.BS_User_No IS NULL";
                    break;
            }
        }
        sql += " ORDER BY b.Del_Date DESC, b.ID_KEY DESC";

        var all = db.Query<BlacklistItemDto>(sql, parameters.ToArray());
        if (releaseable.HasValue) all = all.Where(i => i.releaseable == releaseable.Value).ToList();
        return Api.Ok(new { total = all.Count, items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList() });
    }

    /// <summary>GET api/system/blacklist/{id}</summary>
    [HttpGet("{id:int}")]
    [RequirePermission("system:blacklist:view")]
    public IActionResult Get(int id)
    {
        using var db = SqlAccess.Open();
        var row = db.QuerySingle<BlacklistItemDto>(BaseSelect + " WHERE b.ID_KEY = @id", SqlSession.P("@id", id));
        if (row == null) return Api.NotFound("黑名单记录不存在");
        return Api.Ok(row);
    }

    /// <summary>GET api/system/blacklist/card/{cardNo}：按卡号反查人员与卡状态。</summary>
    [HttpGet("card/{cardNo}")]
    [RequirePermission("system:blacklist:view")]
    public IActionResult CardLookup(string cardNo)
    {
        var no = (cardNo ?? "").Trim();
        if (no.Length == 0) return Api.BadRequest("请输入卡号");

        using var db = SqlAccess.Open();
        var person = db.QuerySingle<CardLookupDto>(
            @"SELECT @card AS cardNo, p.Person_ID AS personId, p.Person_No AS personNo, p.Person_Name AS personName,
                     d.Dept_Name AS deptName, CAST(c.Card_State AS VARCHAR(4)) AS cardState, c.Card_ValidDate AS validDate,
                     " + RetainDaysExpr + @" AS retainDays
                FROM dbo.ST_Person p
                LEFT JOIN dbo.ST_Department d ON d.Dept_ID = p.Dept_ID
                LEFT JOIN dbo.ST_Card c ON c.Person_ID = p.Person_ID
               WHERE p.Card_No = @card AND ISNULL(p.Is_Del, 0) = 0",
            SqlSession.P("@card", no));

        if (person == null)
            return Api.Ok(new { exists = false, cardNo = no, message = "该卡号未登记，仍可加入黑名单" });

        person.exists = true;
        person.inBlacklist = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_BlackPerson WHERE Card_No = @card",
            SqlSession.P("@card", no)) > 0;
        return Api.Ok(person);
    }

    /// <summary>POST api/system/blacklist：加入黑名单并同步卡状态。</summary>
    [HttpPost]
    [RequirePermission("system:blacklist:create")]
    public IActionResult Add([FromBody] BlacklistAddRequest request)
    {
        var cardNo = (request?.Card_No ?? "").Trim();
        if (cardNo.Length == 0) return Api.BadRequest("卡号不能为空");
        if (cardNo.Length > 12) return Api.BadRequest("卡号长度不得超过 12 位");
        var reason = (request!.Reason ?? "").Trim();
        if (reason.Length == 0) return Api.BadRequest("请填写加入原因");

        using var db = SqlAccess.Open();
        var duplicate = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_BlackPerson WHERE Card_No = @card",
            SqlSession.P("@card", cardNo));
        if (duplicate > 0) return Api.StateConflict("该卡号已在黑名单中");

        var person = db.QuerySingle<CardLookupDto>(
            @"SELECT p.Person_ID AS personId, p.Person_No AS personNo, p.Person_Name AS personName,
                     CAST(c.Card_State AS VARCHAR(4)) AS cardState
                FROM dbo.ST_Person p LEFT JOIN dbo.ST_Card c ON c.Person_ID = p.Person_ID
               WHERE p.Card_No = @card AND ISNULL(p.Is_Del, 0) = 0",
            SqlSession.P("@card", cardNo));

        var personId = person?.personId ?? request.Person_ID;
        var delSign = request.Del_Sign ?? false;

        db.Begin();
        try
        {
            db.Exec(
                @"INSERT INTO dbo.ST_BlackPerson (Person_ID, Card_No, Del_Date, Del_Sign, Remark, BS_User_No, BS_Add_Time)
                  VALUES (@pid, @card, GETDATE(), @sign, @remark, @user, GETDATE())",
                SqlSession.P("@pid", (object?)personId ?? DBNull.Value), SqlSession.P("@card", cardNo),
                SqlSession.P("@sign", delSign), SqlSession.P("@remark", Truncate(reason, 50)),
                SqlSession.P("@user", HttpContext.User.GetUserNo()));

            var id = Convert.ToInt32(db.Scalar(
                "SELECT MAX(ID_KEY) FROM dbo.ST_BlackPerson WHERE Card_No = @card", SqlSession.P("@card", cardNo)));

            // 卡状态置 9：person_id 为空时匹配不到任何行，属于预期（卡未登记）
            var cardUpdated = 0;
            if (personId is > 0)
            {
                cardUpdated = db.Exec(
                    "UPDATE dbo.ST_Card SET Card_State = '9' WHERE Person_ID = @pid AND Card_State = '0'",
                    SqlSession.P("@pid", personId!.Value));
            }

            var syncCreated = false;
            if (request.SyncNow == true)
            {
                db.Exec(
                    @"INSERT INTO dbo.BS_SyncLog (Task_Code, Sync_Type, Direction, Target, Target_Key, Status, Message, Trace_ID, Create_Time)
                      VALUES ('PERSON_NAME_DOWN', 'person', 'down', 'MOC_API', @card, 0, N'黑名单变更触发名单下发', @trace, GETDATE())",
                    SqlSession.P("@card", cardNo), SqlSession.P("@trace", HttpContext.TraceIdentifier));
                syncCreated = true;
            }

            db.Commit();
            ConfigAudit.Write(HttpContext, "blacklist", "add", "blackPerson", id.ToString(CultureInfo.InvariantCulture),
                null, new { cardNo, personId, reason, delSign, syncNow = syncCreated });
            return Api.Ok(new { id, cardNo, personId, cardState = cardUpdated > 0 ? "9" : person?.cardState, syncTaskCreated = syncCreated });
        }
        catch
        {
            db.Rollback();
            throw;
        }
    }

    /// <summary>POST api/system/blacklist/import：按卡号批量加入。</summary>
    [HttpPost("import")]
    [RequirePermission("system:blacklist:create")]
    public IActionResult Import([FromBody] BlacklistImportRequest request)
    {
        if (request?.Items.Count == 0) return Api.BadRequest("导入内容为空");
        var added = 0;
        var skipped = 0;
        var errors = new List<string>();

        using var db = SqlAccess.Open();
        foreach (var item in request!.Items)
        {
            var cardNo = (item.cardNo ?? "").Trim();
            if (cardNo.Length == 0) { skipped++; continue; }
            if (db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_BlackPerson WHERE Card_No = @card", SqlSession.P("@card", cardNo)) > 0)
            {
                skipped++;
                errors.Add($"{cardNo} 已在黑名单");
                continue;
            }
            var personId = db.Scalar("SELECT TOP 1 Person_ID FROM dbo.ST_Person WHERE Card_No = @card AND ISNULL(Is_Del,0) = 0",
                SqlSession.P("@card", cardNo));
            db.Exec(
                @"INSERT INTO dbo.ST_BlackPerson (Person_ID, Card_No, Del_Date, Del_Sign, Remark, BS_User_No, BS_Add_Time)
                  VALUES (@pid, @card, GETDATE(), 0, @remark, @user, GETDATE())",
                SqlSession.P("@pid", personId), SqlSession.P("@card", cardNo),
                SqlSession.P("@remark", Truncate(item.reason ?? "批量导入", 50)),
                SqlSession.P("@user", HttpContext.User.GetUserNo()));
            added++;
        }

        ConfigAudit.Write(HttpContext, "blacklist", "import", "blackPerson", null, null, new { added, skipped });
        return Api.Ok(new { added, skipped, errors }, $"新增 {added} 条，跳过 {skipped} 条");
    }

    /// <summary>DELETE api/system/blacklist/{id}：释放（物理删除），并按规则恢复卡状态。</summary>
    [HttpDelete("{id:int}")]
    [RequirePermission("system:blacklist:delete")]
    public IActionResult Release(int id, [FromBody] BlacklistReleaseRequest? request, [FromQuery] string? reasonText)
    {
        var reason = (request?.Reason ?? reasonText ?? "").Trim();
        if (reason.Length == 0) return Api.BadRequest("请填写释放原因");

        using var db = SqlAccess.Open();
        var row = db.QuerySingle<BlacklistItemDto>(BaseSelect + " WHERE b.ID_KEY = @id", SqlSession.P("@id", id));
        if (row == null) return Api.NotFound("黑名单记录不存在");
        if (!row.releaseable)
        {
            var remain = row.releaseableTime.HasValue
                ? Math.Max(0, (int)Math.Ceiling((row.releaseableTime.Value - DateTime.Now).TotalDays))
                : 0;
            return Api.StateConflict($"该记录为人工加入，保留期未到，还需 {remain} 天");
        }

        db.Begin();
        try
        {
            db.Exec("DELETE FROM dbo.ST_BlackPerson WHERE ID_KEY = @id", SqlSession.P("@id", id));

            string? newState = row.cardState;
            if (row.personId is > 0)
            {
                var other = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_BlackPerson WHERE Person_ID = @pid",
                    SqlSession.P("@pid", row.personId!.Value));
                if (other == 0)
                {
                    var restored = db.Exec(
                        @"UPDATE dbo.ST_Card SET Card_State = '0'
                           WHERE Person_ID = @pid AND Card_State = '9'
                             AND (Card_ValidDate IS NULL OR Card_ValidDate >= GETDATE())",
                        SqlSession.P("@pid", row.personId!.Value));
                    if (restored > 0) newState = "0";
                }
            }

            db.Commit();
            ConfigAudit.Write(HttpContext, "blacklist", "release", "blackPerson", id.ToString(CultureInfo.InvariantCulture),
                new { row.cardNo, row.personId, row.delDate, row.remark, row.delSign }, new { reason, cardState = newState });
            return Api.Ok(new { id, cardNo = row.cardNo, cardState = newState, released = true });
        }
        catch
        {
            db.Rollback();
            throw;
        }
    }

    /// <summary>GET api/system/blacklist/export</summary>
    [HttpGet("export")]
    [RequirePermission("system:blacklist:export")]
    public IActionResult Export([FromQuery] string? cardNo, [FromQuery] string? personName)
    {
        using var db = SqlAccess.Open();
        var sql = BaseSelect + " WHERE 1 = 1";
        var parameters = new List<Microsoft.Data.SqlClient.SqlParameter>();
        if (!string.IsNullOrWhiteSpace(cardNo))
        {
            sql += " AND b.Card_No LIKE @card";
            parameters.Add(SqlSession.P("@card", "%" + cardNo.Trim() + "%"));
        }
        if (!string.IsNullOrWhiteSpace(personName))
        {
            sql += " AND p.Person_Name LIKE @name";
            parameters.Add(SqlSession.P("@name", "%" + personName.Trim() + "%"));
        }
        sql += " ORDER BY b.Del_Date DESC";

        var rows = db.Query<BlacklistItemDto>(sql, parameters.ToArray());
        var sb = new StringBuilder();
        sb.AppendLine("卡号,人员编号,姓名,部门,加入时间,来源,原因,状态,可释放时间");
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",",
                Escape(row.cardNo), Escape(row.personNo), Escape(row.personName), Escape(row.deptName),
                row.delDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                row.source switch { "system" => "系统自动", "cs" => "CS 写入", _ => "人工" },
                Escape(row.remark), row.cardState ?? "", row.releaseableTime?.ToString("yyyy-MM-dd") ?? ""));
        }

        ConfigAudit.Write(HttpContext, "blacklist", "export", "blackPerson", null, null, new { itemCount = rows.Count });
        return Api.Ok(new { fileName = $"blacklist-{DateTime.Now:yyyyMMddHHmmss}.csv", content = sb.ToString(), itemCount = rows.Count });
    }

    private static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Contains(',') || text.Contains('"') ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }

    private static string Truncate(string? value, int max)
    {
        if (value == null) return "";
        return value.Length <= max ? value : value[..max];
    }
}
