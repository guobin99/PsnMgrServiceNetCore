using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.Card;

/// <summary>
/// 人员档案：查询、新增、修改、注销、恢复、批量调动、导出。
/// 主数据仍写 ST_Person；卡号与账户只读展示，开户/销户在账户与卡模块完成。
/// </summary>
[Route("api/card/persons")]
[ApiController]
public class CardPersonsController : ControllerBase
{
    private readonly AuditService _audit;
    private readonly PermissionService _permission;

    public CardPersonsController(AuditService audit, PermissionService permission)
    {
        _audit = audit;
        _permission = permission;
    }

    private const string BaseSelect = @"
SELECT p.Person_ID              AS personId,
       p.Person_No              AS personNo,
       p.Person_Name            AS personName,
       p.Dept_ID                AS deptId,
       d.Dept_No                AS deptNo,
       d.Dept_Name              AS deptName,
       p.Card_No                AS cardNo,
       p.Person_Type            AS personType,
       (SELECT TOP 1 Dict_Value FROM dbo.ST_Dictionary WHERE Table_Name = 'BM_PERSONTYPE' AND Dict_Id = p.Person_Type) AS personTypeName,
       p.Cert_Name              AS certName,
       p.Cert_Code              AS certCode,
       p.Duty                   AS duty,
       (SELECT TOP 1 Dict_Value FROM dbo.ST_Dictionary WHERE Table_Name = 'BM_DUTY' AND Dict_Id = p.Duty) AS dutyName,
       p.Title                  AS title,
       p.Nationality            AS nationality,
       (SELECT TOP 1 Dict_Value FROM dbo.ST_Dictionary WHERE Table_Name = 'BM_NATIONALITY' AND Dict_Id = p.Nationality) AS nationalityName,
       p.Sex                    AS sex,
       p.Birthday               AS birthday,
       p.Is_Marry               AS isMarry,
       p.NativePlace            AS nativePlace,
       p.Address                AS address,
       p.Residence              AS residence,
       p.OnWork_Date            AS onWorkDate,
       p.OffWork_Date           AS offWorkDate,
       p.Is_Del                 AS isDel,
       p.Remark                 AS remark,
       p.mobile                 AS mobile,
       p.id_number              AS idNumber,
       p.update_time            AS updateTime,
       CASE WHEN DATALENGTH(ph.Photo) > 0 THEN 1 ELSE 0 END AS hasPhoto,
       c.Type_No                AS cardTypeNo,
       ct.Type_Name             AS cardTypeName,
       c.Card_State             AS cardState,
       c.Card_Balance           AS cardBalance,
       ISNULL(ext.BS_Face_Status, 0) AS faceStatus
  FROM dbo.ST_Person p
  INNER JOIN dbo.ST_Department d  ON d.Dept_ID = p.Dept_ID
  LEFT JOIN dbo.ST_PersonPhoto ph ON ph.Person_ID = p.Person_ID
  LEFT JOIN dbo.ST_Card c        ON c.Person_ID = p.Person_ID
  LEFT JOIN dbo.ST_CardType ct   ON ct.Type_No = c.Type_No
  LEFT JOIN dbo.BS_PersonExt ext ON ext.Person_ID = p.Person_ID";

    /// <summary>GET api/card/persons：分页查询，默认只查在册人员。</summary>
    [HttpGet]
    [RequirePermission("card:person:view")]
    public async Task<IActionResult> List(
        [FromQuery] string? keyword,
        [FromQuery] int? deptId,
        [FromQuery] bool includeChildren = true,
        [FromQuery] string? personType = null,
        [FromQuery] string? status = null,
        [FromQuery] bool? hasPhoto = null,
        [FromQuery] string? updateFrom = null,
        [FromQuery] string? updateTo = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = CardRules.ClampPage(page);
        pageSize = CardRules.ClampPageSize(pageSize);

        using var db = SqlAccess.Open();
        var parameters = new List<SqlParameter>();
        var where = BuildFilter(db, keyword, deptId, includeChildren, personType, status, hasPhoto, updateFrom, updateTo, parameters);

        var total = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_Person p LEFT JOIN dbo.ST_Department d ON d.Dept_ID = p.Dept_ID " +
                                 "LEFT JOIN dbo.ST_PersonPhoto ph ON ph.Person_ID = p.Person_ID WHERE " + where, parameters.ToArray());

        // 排序字段用内层派生表的别名（兼容级别 90 下 ROW_NUMBER 排序只能引用派生表列）。
        // 只在白名单内取值，避免拼接注入；卡号可能为空，空值统一排到末尾。
        var order = (sortBy ?? string.Empty).ToLowerInvariant() switch
        {
            "update" => "q.updateTime DESC, q.personNo",
            "personno" => "q.personNo, q.deptNo",
            "personname" => "q.personName, q.personNo",
            "cardno" => "CASE WHEN ISNULL(q.cardNo, '') = '' THEN 1 ELSE 0 END, q.cardNo, q.personNo",
            _ => "q.deptNo, q.personNo",
        };

        parameters.Add(SqlSession.P("@offset", (page - 1) * pageSize));
        parameters.Add(SqlSession.P("@size", pageSize));

        // 生产线库为 SQL Server 2008 R2（兼容级别 90），不支持 OFFSET/FETCH，统一用 ROW_NUMBER 分页。
        var items = db.Query<CardPersonRow>(@"
SELECT * FROM (
    SELECT q.*, ROW_NUMBER() OVER (ORDER BY " + order + @") AS RowNum
    FROM (
" + BaseSelect + @"
 WHERE " + where + @"
    ) q
) t
WHERE t.RowNum > @offset AND t.RowNum <= @offset + @size
ORDER BY t.RowNum", parameters.ToArray());

        await ApplyMaskAsync(items);
        return Api.Ok(new { total, items });
    }

    /// <summary>GET api/card/persons/next-no：预取下一个人员编号，不占号。</summary>
    [HttpGet("next-no")]
    [RequirePermission("card:person:create")]
    public IActionResult NextNo()
    {
        using var db = SqlAccess.Open();
        return Api.Ok(new { personNo = CardPersonNo.Peek(db) });
    }

    /// <summary>
    /// GET api/card/persons/types：人员类型候选值。
    /// ST_Person.Person_Type 是自由文本（现场库 BM_PERSONTYPE 字典为空），
    /// 因此按实际已用的值去重统计，供下拉建议；录入仍允许任意新值。
    /// </summary>
    [HttpGet("types")]
    [RequirePermission("card:person:view")]
    public IActionResult Types()
    {
        using var db = SqlAccess.Open();
        var items = db.Query<CardPersonTypeRow>(@"
SELECT p.Person_Type AS personType, COUNT(1) AS personCount
  FROM dbo.ST_Person p
 WHERE ISNULL(p.Person_Type, '') <> ''
 GROUP BY p.Person_Type
 ORDER BY COUNT(1) DESC, p.Person_Type");
        return Api.Ok(new { items });
    }

    /// <summary>GET api/card/persons/export：按筛选条件导出 xlsx（敏感字段按权限脱敏）。</summary>
    [HttpGet("export")]
    [RequirePermission("card:person:export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? keyword,
        [FromQuery] int? deptId,
        [FromQuery] bool includeChildren = true,
        [FromQuery] string? personType = null,
        [FromQuery] string? status = null,
        [FromQuery] bool? hasPhoto = null,
        [FromQuery] string? updateFrom = null,
        [FromQuery] string? updateTo = null)
    {
        const int limit = 50000;
        using var db = SqlAccess.Open();
        var parameters = new List<SqlParameter>();
        var where = BuildFilter(db, keyword, deptId, includeChildren, personType, status, hasPhoto, updateFrom, updateTo, parameters);
        var rows = db.Query<CardPersonRow>(BaseSelect + " WHERE " + where + " ORDER BY d.Dept_No, p.Person_No", parameters.ToArray());
        if (rows.Count > limit) return Api.BadRequest($"导出上限 {limit} 行，请缩小查询范围");

        await ApplyMaskAsync(rows);
        var table = new List<string[]>
        {
            new[] { "人员编号", "姓名", "部门", "卡号", "人员类型", "性别", "出生日期", "手机", "身份证号", "状态", "更新时间" },
        };
        foreach (var row in rows)
        {
            table.Add(new[]
            {
                row.personNo ?? string.Empty,
                row.personName ?? string.Empty,
                row.deptName ?? string.Empty,
                string.IsNullOrEmpty(row.cardNo) ? "未发卡" : row.cardNo,
                row.personTypeName ?? row.personType ?? string.Empty,
                CardRules.SexText(row.sex),
                row.birthday?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                row.mobile ?? string.Empty,
                row.idNumber ?? string.Empty,
                row.isDel ? "已注销" : "在册",
                row.updateTime?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? string.Empty,
            });
        }

        var bytes = CardXlsx.Build("人员档案", table);
        _audit.Log(HttpContext, "card-person", "export", "person", null, null,
            new { total = rows.Count, keyword, deptId, status });
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"人员档案_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }

    /// <summary>GET api/card/persons/{id}：详情，含卡摘要与子系统权限。</summary>
    [HttpGet("{id:int}")]
    [RequirePermission("card:person:view")]
    public async Task<IActionResult> Detail(int id)
    {
        using var db = SqlAccess.Open();
        var row = db.QuerySingle<CardPersonRow>(BaseSelect + " WHERE p.Person_ID = @id", SqlSession.P("@id", id));
        if (row == null) return Api.NotFound("人员不存在");

        var subsystems = db.Query<CardSubSystemRef>(
            "SELECT System_No AS systemNo FROM dbo.ST_SubSysRight WHERE Person_ID = @id", SqlSession.P("@id", id));
        var photo = db.QuerySingle<CardPhotoMeta>(@"
            SELECT DATALENGTH(Photo) AS photoBytes, DATALENGTH(id_photo) AS idPhotoBytes, update_time AS updateTime
              FROM dbo.ST_PersonPhoto WHERE Person_ID = @id", SqlSession.P("@id", id));

        await ApplyMaskAsync(new List<CardPersonRow> { row });

        return Api.Ok(new
        {
            person = row,
            systems = subsystems.Select(s => s.systemNo).ToList(),
            photo = new
            {
                hasPhoto = (photo?.photoBytes ?? 0) > 0,
                photoBytes = photo?.photoBytes ?? 0,
                idPhotoBytes = photo?.idPhotoBytes ?? 0,
                updateTime = photo?.updateTime,
            },
        });
    }

    /// <summary>POST api/card/persons：新增人员，事务内发号并写子系统权限。</summary>
    [HttpPost]
    [RequirePermission("card:person:create")]
    public IActionResult Create([FromBody] CardPersonSaveRequest request)
    {
        var validation = ValidateSave(request);
        if (validation != null) return validation;

        using var db = SqlAccess.Open();
        var error = ValidateBusiness(db, request, null);
        if (error != null) return error;

        db.Begin();
        try
        {
            string? personNo;
            if (!string.IsNullOrWhiteSpace(request.PersonNo))
            {
                personNo = CardRules.Trim(request.PersonNo);
            }
            else
            {
                string allocError;
                personNo = CardPersonNo.Next(db, out allocError);
                if (personNo == null)
                {
                    db.Rollback();
                    return Api.Fail(400, "40007", allocError ?? "人员编号生成失败");
                }
            }

            var personId = CardPersonNo.NextPersonId(db);
            db.Exec(@"
INSERT INTO dbo.ST_Person
    (Person_ID, Dept_ID, Card_No, Person_No, Person_Name, Person_Type, Cert_Name, Cert_Code, Duty, Title,
     Nationality, Sex, Birthday, Is_Marry, NativePlace, Address, Residence, OnWork_Date, OffWork_Date,
     Is_Del, Remark, update_time, mobile, id_number)
VALUES
    (@personId, @deptId, '', @personNo, @personName, @personType, @certName, @certCode, @duty, @title,
     @nationality, @sex, @birthday, @isMarry, @nativePlace, @address, @residence, @onWorkDate, @offWorkDate,
     0, @remark, GETDATE(), @mobile, @idNumber)",
                SqlSession.P("@personId", personId),
                SqlSession.P("@deptId", request.DeptId),
                SqlSession.P("@personNo", personNo),
                SqlSession.P("@personName", Cut(request.PersonName, 20)),
                SqlSession.P("@personType", Cut(request.PersonType, 20)),
                SqlSession.P("@certName", Cut(request.CertName, 20)),
                SqlSession.P("@certCode", Cut(request.CertCode, 20)),
                SqlSession.P("@duty", Cut(request.Duty, 20)),
                SqlSession.P("@title", Cut(request.Title, 20)),
                SqlSession.P("@nationality", Cut(request.Nationality, 2)),
                SqlSession.P("@sex", Cut(CardRules.NormalizeSex(request.Sex), 2)),
                SqlSession.P("@birthday", CardRules.ParseDate(request.Birthday)),
                SqlSession.P("@isMarry", request.IsMarry ?? false),
                SqlSession.P("@nativePlace", Cut(request.NativePlace, 50)),
                SqlSession.P("@address", Cut(request.Address, 50)),
                SqlSession.P("@residence", Cut(request.Residence, 30)),
                SqlSession.P("@onWorkDate", CardRules.NormalizeDate(request.OnWorkDate)),
                SqlSession.P("@offWorkDate", CardRules.NormalizeDate(request.OffWorkDate)),
                SqlSession.P("@remark", Cut(request.Remark, 50)),
                SqlSession.P("@mobile", Cut(request.Mobile, 30)),
                SqlSession.P("@idNumber", Cut(request.IdNumber, 30)));

            SaveSubSystems(db, personId, request.Systems);
            db.Exec(@"
IF NOT EXISTS (SELECT 1 FROM dbo.BS_PersonExt WHERE Person_ID = @id)
    INSERT INTO dbo.BS_PersonExt (Person_ID, BS_Source, BS_Face_Status, BS_Create_User, BS_Create_Time, BS_Update_User, BS_Update_Time)
    VALUES (@id, @source, 0, @user, GETDATE(), @user, GETDATE())
ELSE
    UPDATE dbo.BS_PersonExt SET BS_Update_User = @user, BS_Update_Time = GETDATE() WHERE Person_ID = @id",
                SqlSession.P("@id", personId),
                SqlSession.P("@source", string.IsNullOrWhiteSpace(request.Source) ? "BS" : Cut(request.Source, 20)),
                SqlSession.P("@user", CurrentUser));

            db.Commit();
            _audit.Log(HttpContext, "card-person", "create", "person", personId.ToString(CultureInfo.InvariantCulture),
                null, new { personNo, request.PersonName, request.DeptId, systems = request.Systems });
            return Api.Ok(new { personId, personNo, deptId = request.DeptId });
        }
        catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
        {
            db.Rollback();
            return Api.Fail(400, "40004", "人员编号或卡号已存在");
        }
        catch (Exception)
        {
            db.Rollback();
            throw;
        }
    }

    /// <summary>PUT api/card/persons/{id}：字段级更新，只写发生变化的列。</summary>
    [HttpPut("{id:int}")]
    [RequirePermission("card:person:update")]
    public IActionResult Update(int id, [FromBody] CardPersonSaveRequest request)
    {
        using var db = SqlAccess.Open();
        var current = db.QuerySingle<CardPersonRow>(BaseSelect + " WHERE p.Person_ID = @id", SqlSession.P("@id", id));
        if (current == null) return Api.NotFound("人员不存在");
        if (current.isDel) return Api.StateConflict("人员已注销，请先恢复后再修改");

        var validation = ValidateSave(request);
        if (validation != null) return validation;

        var business = ValidateBusiness(db, request, id);
        if (business != null) return business;

        var sets = new List<string>();
        var parameters = new List<SqlParameter> { SqlSession.P("@id", id) };

        void Add(string column, string value, string? original, SqlParameter parameter)
        {
            if (string.Equals(value ?? string.Empty, original ?? string.Empty, StringComparison.Ordinal)) return;
            sets.Add(column + " = " + parameter.ParameterName);
            parameters.Add(parameter);
        }

        if (!string.IsNullOrWhiteSpace(request.PersonNo))
        {
            Add("Person_No", CardRules.Trim(request.PersonNo), current.personNo, SqlSession.P("@personNo", CardRules.Trim(request.PersonNo)));
        }
        Add("Person_Name", CardRules.Trim(request.PersonName), current.personName, SqlSession.P("@personName", Cut(request.PersonName, 20)));
        Add("Person_Type", CardRules.Trim(request.PersonType), current.personType, SqlSession.P("@personType", Cut(request.PersonType, 20)));
        Add("Cert_Name", CardRules.Trim(request.CertName), current.certName, SqlSession.P("@certName", Cut(request.CertName, 20)));
        Add("Cert_Code", CardRules.Trim(request.CertCode), current.certCode, SqlSession.P("@certCode", Cut(request.CertCode, 20)));
        Add("Duty", CardRules.Trim(request.Duty), current.duty, SqlSession.P("@duty", Cut(request.Duty, 20)));
        Add("Title", CardRules.Trim(request.Title), current.title, SqlSession.P("@title", Cut(request.Title, 20)));
        Add("Nationality", CardRules.Trim(request.Nationality), current.nationality, SqlSession.P("@nationality", Cut(request.Nationality, 2)));
        Add("Sex", CardRules.NormalizeSex(request.Sex), current.sex, SqlSession.P("@sex", Cut(CardRules.NormalizeSex(request.Sex), 2)));
        Add("NativePlace", CardRules.Trim(request.NativePlace), current.nativePlace, SqlSession.P("@nativePlace", Cut(request.NativePlace, 50)));
        Add("Address", CardRules.Trim(request.Address), current.address, SqlSession.P("@address", Cut(request.Address, 50)));
        Add("Residence", CardRules.Trim(request.Residence), current.residence, SqlSession.P("@residence", Cut(request.Residence, 30)));
        Add("Remark", CardRules.Trim(request.Remark), current.remark, SqlSession.P("@remark", Cut(request.Remark, 50)));
        Add("mobile", CardRules.Trim(request.Mobile), current.mobile, SqlSession.P("@mobile", Cut(request.Mobile, 30)));
        Add("id_number", CardRules.Trim(request.IdNumber), current.idNumber, SqlSession.P("@idNumber", Cut(request.IdNumber, 30)));
        Add("OnWork_Date", CardRules.NormalizeDate(request.OnWorkDate), current.onWorkDate, SqlSession.P("@onWorkDate", CardRules.NormalizeDate(request.OnWorkDate)));
        Add("OffWork_Date", CardRules.NormalizeDate(request.OffWorkDate), current.offWorkDate, SqlSession.P("@offWorkDate", CardRules.NormalizeDate(request.OffWorkDate)));

        if (request.Birthday != null || current.birthday != null)
        {
            var birthday = CardRules.ParseDate(request.Birthday);
            if (birthday != current.birthday)
            {
                sets.Add("Birthday = @birthday");
                parameters.Add(SqlSession.P("@birthday", birthday));
            }
        }

        var isMarry = request.IsMarry ?? false;
        if (isMarry != (current.isMarry ?? false))
        {
            sets.Add("Is_Marry = @isMarry");
            parameters.Add(SqlSession.P("@isMarry", isMarry));
        }

        if (request.DeptId != current.deptId)
        {
            sets.Add("Dept_ID = @deptId");
            parameters.Add(SqlSession.P("@deptId", request.DeptId));
        }

        if (sets.Count == 0)
        {
            return Api.Ok(new { personId = id, changed = 0 }, "没有需要保存的修改");
        }

        sets.Add("update_time = GETDATE()");
        var sql = "UPDATE dbo.ST_Person SET " + string.Join(", ", sets) + " WHERE Person_ID = @id AND ISNULL(Is_Del, 0) = 0";
        if (current.updateTime != null)
        {
            sql += " AND update_time = @oldTime";
            parameters.Add(SqlSession.P("@oldTime", current.updateTime));
        }

        var affected = db.Exec(sql, parameters.ToArray());
        if (affected == 0) return Api.Fail(409, "40903", "人员已被其他操作修改，请刷新后重试");

        if (request.Systems != null)
        {
            SaveSubSystems(db, id, request.Systems);
        }

        db.Exec(@"
IF NOT EXISTS (SELECT 1 FROM dbo.BS_PersonExt WHERE Person_ID = @id)
    INSERT INTO dbo.BS_PersonExt (Person_ID, BS_Source, BS_Face_Status, BS_Update_User, BS_Update_Time)
    VALUES (@id, 'BS', 0, @user, GETDATE())
ELSE
    UPDATE dbo.BS_PersonExt SET BS_Update_User = @user, BS_Update_Time = GETDATE() WHERE Person_ID = @id",
            SqlSession.P("@id", id), SqlSession.P("@user", CurrentUser));

        var after = db.QuerySingle<CardPersonRow>(BaseSelect + " WHERE p.Person_ID = @id", SqlSession.P("@id", id));
        _audit.Log(HttpContext, "card-person", "update", "person", id.ToString(CultureInfo.InvariantCulture), current, after);
        return Api.Ok(new { personId = id, changed = sets.Count - 1 });
    }

    /// <summary>DELETE api/card/persons/{id}：注销（逻辑删除），必须先销户。</summary>
    [HttpDelete("{id:int}")]
    [RequirePermission("card:person:delete")]
    public IActionResult Delete(int id, [FromQuery] string? reason)
    {
        using var db = SqlAccess.Open();
        var current = db.QuerySingle<CardPersonRow>(BaseSelect + " WHERE p.Person_ID = @id", SqlSession.P("@id", id));
        if (current == null) return Api.NotFound("人员不存在");
        if (current.isDel) return Api.StateConflict("人员已注销");

        var cardCount = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_Card WHERE Person_ID = @id", SqlSession.P("@id", id));
        if (cardCount > 0) return Api.Fail(409, "40907", "该人员存在未销户账户，请先在账户与卡模块销户后再注销");

        db.Exec("UPDATE dbo.ST_Person SET Is_Del = 1, update_time = GETDATE() WHERE Person_ID = @id AND ISNULL(Is_Del, 0) = 0",
            SqlSession.P("@id", id));
        db.Exec(@"IF NOT EXISTS (SELECT 1 FROM dbo.BS_PersonExt WHERE Person_ID = @id)
                      INSERT INTO dbo.BS_PersonExt (Person_ID, BS_Source, BS_Face_Status, BS_Update_User, BS_Update_Time)
                      VALUES (@id, 'BS', 0, @user, GETDATE())
                  ELSE UPDATE dbo.BS_PersonExt SET BS_Update_User = @user, BS_Update_Time = GETDATE() WHERE Person_ID = @id",
            SqlSession.P("@id", id), SqlSession.P("@user", CurrentUser));

        _audit.Log(HttpContext, "card-person", "delete", "person", id.ToString(CultureInfo.InvariantCulture),
            new { isDel = false }, new { isDel = true, reason });
        return Api.Ok(new { personId = id });
    }

    /// <summary>POST api/card/persons/{id}/restore：恢复已注销人员，重新校验编号唯一。</summary>
    [HttpPost("{id:int}/restore")]
    [RequirePermission("card:person:delete")]
    public IActionResult Restore(int id)
    {
        using var db = SqlAccess.Open();
        var current = db.QuerySingle<CardPersonRow>(BaseSelect + " WHERE p.Person_ID = @id", SqlSession.P("@id", id));
        if (current == null) return Api.NotFound("人员不存在");
        if (!current.isDel) return Api.StateConflict("人员当前为在册状态");

        var duplicated = db.ScalarInt(
            "SELECT COUNT(1) FROM dbo.ST_Person WHERE Person_No = @no AND ISNULL(Is_Del, 0) = 0 AND Person_ID <> @id",
            SqlSession.P("@no", current.personNo), SqlSession.P("@id", id));
        if (duplicated > 0) return Api.Fail(400, "40004", $"人员编号 {current.personNo} 已被在册人员占用，无法恢复");

        db.Exec("UPDATE dbo.ST_Person SET Is_Del = 0, update_time = GETDATE() WHERE Person_ID = @id",
            SqlSession.P("@id", id));
        _audit.Log(HttpContext, "card-person", "restore", "person", id.ToString(CultureInfo.InvariantCulture),
            new { isDel = true }, new { isDel = false });
        return Api.Ok(new { personId = id });
    }

    /// <summary>POST api/card/persons/move：批量调动。</summary>
    [HttpPost("move")]
    [RequirePermission("card:person:update")]
    public IActionResult Move([FromBody] CardPersonMoveRequest request)
    {
        if (request == null || request.PersonIds == null || request.PersonIds.Count == 0)
            return Api.BadRequest("请选择需要调动的人员");
        if (request.PersonIds.Count > 500) return Api.BadRequest("单次调动不得超过 500 人");

        using var db = SqlAccess.Open();
        var dept = db.QuerySingle<CardDeptBrief>("SELECT Dept_ID AS deptId, Dept_No AS deptNo, Dept_Name AS deptName, Is_Del AS isDel " +
                                                 "FROM dbo.ST_Department WHERE Dept_ID = @id", SqlSession.P("@id", request.DeptId));
        if (dept == null) return Api.BadRequest("目标部门不存在");
        if (dept.isDel == true) return Api.BadRequest("目标部门已停用，不能调入");

        db.Begin();
        try
        {
            var affected = 0;
            foreach (var personId in request.PersonIds.Distinct())
            {
                affected += db.Exec(
                    "UPDATE dbo.ST_Person SET Dept_ID = @deptId, update_time = GETDATE() WHERE Person_ID = @id AND ISNULL(Is_Del, 0) = 0",
                    SqlSession.P("@deptId", request.DeptId), SqlSession.P("@id", personId));
            }
            db.Commit();

            RefreshDeptTotal(db);
            _audit.Log(HttpContext, "card-person", "move", "person", string.Join(",", request.PersonIds.Take(20)),
                null, new { deptId = request.DeptId, affected });
            return Api.Ok(new { affected, deptId = request.DeptId });
        }
        catch (Exception)
        {
            db.Rollback();
            throw;
        }
    }

    /* ---------------- 内部方法 ---------------- */

    private string CurrentUser => HttpContext?.User.GetUserNo() ?? string.Empty;

    private static string Cut(string? value, int length)
    {
        var text = CardRules.Trim(value);
        return text.Length <= length ? text : text.Substring(0, length);
    }

    /// <summary>把筛选条件拼成 WHERE 片段，参数按顺序追加。</summary>
    private static string BuildFilter(
        SqlSession db,
        string? keyword,
        int? deptId,
        bool includeChildren,
        string? personType,
        string? status,
        bool? hasPhoto,
        string? updateFrom,
        string? updateTo,
        List<SqlParameter> parameters)
    {
        var where = new StringBuilder("1 = 1");

        if (string.Equals(status, "deleted", StringComparison.OrdinalIgnoreCase))
            where.Append(" AND p.Is_Del = 1");
        else if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
            where.Append(" AND ISNULL(p.Is_Del, 0) = 0");

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
                if (prefix.Trim().Length == 0) return "1 = 0";
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

        if (!string.IsNullOrWhiteSpace(personType))
        {
            where.Append(" AND p.Person_Type = @personType");
            parameters.Add(SqlSession.P("@personType", personType.Trim()));
        }

        if (hasPhoto.HasValue)
        {
            where.Append(hasPhoto.Value ? " AND DATALENGTH(ph.Photo) > 0" : " AND ISNULL(DATALENGTH(ph.Photo), 0) = 0");
        }

        var from = CardRules.ParseDate(updateFrom);
        if (from.HasValue)
        {
            where.Append(" AND p.update_time >= @from");
            parameters.Add(SqlSession.P("@from", from.Value));
        }
        var to = CardRules.ParseDate(updateTo);
        if (to.HasValue)
        {
            where.Append(" AND p.update_time < @to");
            parameters.Add(SqlSession.P("@to", to.Value.AddDays(1)));
        }

        return where.ToString();
    }

    /// <summary>格式校验：与前端规则一致，服务端仍需独立校验。</summary>
    private IActionResult? ValidateSave(CardPersonSaveRequest request)
    {
        if (request == null) return Api.BadRequest("请求内容不能为空");
        if (!CardRules.IsValidName(request.PersonName)) return Api.BadRequest("姓名必填，且不超过 20 个字符");
        if (request.DeptId == null || request.DeptId <= 0) return Api.BadRequest("请选择所属部门");
        if (!string.IsNullOrWhiteSpace(request.PersonNo) && !CardRules.IsValidPersonNo(request.PersonNo))
            return Api.BadRequest("人员编号需为 2 至 20 位字母或数字");
        if (!CardRules.IsValidMobile(request.Mobile)) return Api.BadRequest("手机号格式不正确");
        if (!CardRules.IsValidIdNumber(request.IdNumber)) return Api.BadRequest("身份证号需为 15 或 18 位");
        if (!string.IsNullOrWhiteSpace(request.OnWorkDate) && CardRules.ParseDate(request.OnWorkDate) == null)
            return Api.BadRequest("参加工作时间格式应为 yyyy-MM-dd");
        if (!string.IsNullOrWhiteSpace(request.OffWorkDate) && CardRules.ParseDate(request.OffWorkDate) == null)
            return Api.BadRequest("离职时间格式应为 yyyy-MM-dd");
        return null;
    }

    private IActionResult? ValidateBusiness(SqlSession db, CardPersonSaveRequest request, int? personId)
    {
        var dept = db.QuerySingle<CardDeptBrief>("SELECT Dept_ID AS deptId, Dept_No AS deptNo, Dept_Name AS deptName, Is_Del AS isDel " +
                                                 "FROM dbo.ST_Department WHERE Dept_ID = @id", SqlSession.P("@id", request.DeptId));
        if (dept == null) return Api.BadRequest("所属部门不存在");
        if (dept.isDel == true) return Api.BadRequest("所属部门已停用");

        if (!string.IsNullOrWhiteSpace(request.PersonNo))
        {
            var personNo = CardRules.Trim(request.PersonNo);
            var duplicated = db.ScalarInt(
                "SELECT COUNT(1) FROM dbo.ST_Person WHERE Person_No = @no AND ISNULL(Is_Del, 0) = 0 AND Person_ID <> @id",
                SqlSession.P("@no", personNo), SqlSession.P("@id", personId ?? 0));
            if (duplicated > 0) return Api.Fail(400, "40004", $"人员编号 {personNo} 已被占用");
        }

        return null;
    }

    /// <summary>整体替换人员的子系统权限，ST 为平台核心，始终保留。</summary>
    private static void SaveSubSystems(SqlSession db, int personId, List<string>? systems)
    {
        var allowed = new HashSet<string>(db.Query<CardSubSystemRef>(
            "SELECT System_No AS systemNo FROM dbo.ST_SubSystem").Select(s => s.systemNo ?? string.Empty),
            StringComparer.OrdinalIgnoreCase) { "ST" };

        var target = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ST" };
        if (systems != null)
        {
            foreach (var system in systems.Where(s => !string.IsNullOrWhiteSpace(s)))
                if (allowed.Contains(system.Trim())) target.Add(system.Trim());
        }

        var existing = db.Query<CardSubSystemRef>(
            "SELECT System_No AS systemNo FROM dbo.ST_SubSysRight WHERE Person_ID = @id", SqlSession.P("@id", personId))
            .Select(s => s.systemNo ?? string.Empty).ToList();

        foreach (var system in existing)
        {
            if (!target.Contains(system))
                db.Exec("DELETE FROM dbo.ST_SubSysRight WHERE Person_ID = @id AND System_No = @system",
                    SqlSession.P("@id", personId), SqlSession.P("@system", system));
        }

        foreach (var system in target)
        {
            if (existing.Contains(system, StringComparer.OrdinalIgnoreCase)) continue;
            db.Exec("INSERT INTO dbo.ST_SubSysRight (Person_ID, System_No) VALUES (@id, @system)",
                SqlSession.P("@id", personId), SqlSession.P("@system", system));
        }
    }

    /// <summary>按权限决定是否脱敏：无 card:person:sensitive 时掩码身份证与手机。</summary>
    private async Task ApplyMaskAsync(List<CardPersonRow> rows)
    {
        if (rows.Count == 0) return;
        var userNo = CurrentUser;
        var sensitive = await _permission.HasPermissionAsync(userNo, "card:person:sensitive");
        if (sensitive) return;
        foreach (var row in rows)
        {
            row.idNumber = CardRules.MaskIdNumber(row.idNumber);
            row.mobile = CardRules.MaskMobile(row.mobile);
        }
    }

    /// <summary>调动或注销后回写部门在册人数，供 CS 列表读取。</summary>
    private static void RefreshDeptTotal(SqlSession db)
    {
        db.Exec(@"
UPDATE sd
   SET Dept_Total = ISNULL(x.Cnt, 0)
  FROM dbo.ST_Department sd
  LEFT JOIN (SELECT Dept_ID, COUNT(1) AS Cnt FROM dbo.ST_Person WHERE ISNULL(Is_Del, 0) = 0 GROUP BY Dept_ID) x
    ON x.Dept_ID = sd.Dept_ID");
    }
}

public class CardPersonSaveRequest
{
    public string? PersonNo { get; set; }
    public string? PersonName { get; set; }
    public int? DeptId { get; set; }
    public string? PersonType { get; set; }
    public string? CertName { get; set; }
    public string? CertCode { get; set; }
    public string? Duty { get; set; }
    public string? Title { get; set; }
    public string? Nationality { get; set; }
    public string? Sex { get; set; }
    public string? Birthday { get; set; }
    public bool? IsMarry { get; set; }
    public string? NativePlace { get; set; }
    public string? Address { get; set; }
    public string? Residence { get; set; }
    public string? OnWorkDate { get; set; }
    public string? OffWorkDate { get; set; }
    public string? Remark { get; set; }
    public string? Mobile { get; set; }
    public string? IdNumber { get; set; }
    public string? Source { get; set; }
    public List<string>? Systems { get; set; }
}

public class CardPersonMoveRequest
{
    public List<int> PersonIds { get; set; } = new();
    public int DeptId { get; set; }
}

public class CardSubSystemRef
{
    public string? systemNo { get; set; }
    public string? systemName { get; set; }
}

public class CardPhotoMeta
{
    public int photoBytes { get; set; }
    public int idPhotoBytes { get; set; }
    public DateTime? updateTime { get; set; }
}

public class CardDeptBrief
{
    public int deptId { get; set; }
    public string? deptNo { get; set; }
    public string? deptName { get; set; }
    public bool? isDel { get; set; }
}
