using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.Card;

/// <summary>
/// 人脸照片库：照片查询、预览、上传/替换、删除、审核、批量导入与统计。
/// 照片写 ST_PersonPhoto（Photo 与 id_photo 同时写），状态写 BS_PersonExt。
/// 特征提取与终端下发仍由 5501 服务与同步任务负责，BS 不生成特征文件。
/// </summary>
[Route("api/card/faces")]
[ApiController]
public class CardFacesController : ControllerBase
{
    private readonly AuditService _audit;
    private readonly PermissionService _permission;

    public CardFacesController(AuditService audit, PermissionService permission)
    {
        _audit = audit;
        _permission = permission;
    }

    private string CurrentUser => HttpContext?.User.GetUserNo() ?? string.Empty;

    /// <summary>GET api/card/faces：照片列表（按人展示）。</summary>
    [HttpGet]
    [RequirePermission("card:face:view")]
    public IActionResult List(
        [FromQuery] string? keyword,
        [FromQuery] int? deptId,
        [FromQuery] bool includeChildren = true,
        [FromQuery] int? faceStatus = null,
        [FromQuery] bool? hasPhoto = null,
        [FromQuery] string? updateFrom = null,
        [FromQuery] string? updateTo = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24)
    {
        page = CardRules.ClampPage(page);
        pageSize = CardRules.ClampPageSize(pageSize, 24);

        using var db = SqlAccess.Open();
        var parameters = new List<SqlParameter>();
        var where = BuildFilter(db, keyword, deptId, includeChildren, faceStatus, hasPhoto, updateFrom, updateTo, parameters);

        var total = db.ScalarInt(@"SELECT COUNT(1) FROM dbo.ST_Person p
  LEFT JOIN dbo.ST_Department d ON d.Dept_ID = p.Dept_ID
  LEFT JOIN dbo.ST_PersonPhoto ph ON ph.Person_ID = p.Person_ID
  LEFT JOIN dbo.BS_PersonExt ext ON ext.Person_ID = p.Person_ID
 WHERE " + where, parameters.ToArray());

        parameters.Add(SqlSession.P("@offset", (page - 1) * pageSize));
        parameters.Add(SqlSession.P("@size", pageSize));

        // 兼容 SQL Server 2008 R2：用 ROW_NUMBER 代替 OFFSET/FETCH。
        var items = db.Query<CardFaceRow>(@"
SELECT * FROM (
    SELECT q.*, ROW_NUMBER() OVER (ORDER BY ISNULL(q.photoTime, CAST('1900-01-01' AS DATETIME)) DESC, q.personNo) AS RowNum
    FROM (
SELECT p.Person_ID AS personId, p.Person_No AS personNo, p.Person_Name AS personName,
       p.Dept_ID AS deptId, d.Dept_No AS deptNo, d.Dept_Name AS deptName,
       CASE WHEN DATALENGTH(ph.Photo) > 0 THEN 1 ELSE 0 END AS hasPhoto,
       ISNULL(ext.BS_Face_Status, 0) AS faceStatus,
       ext.BS_Photo_Review_User AS reviewUser, ext.BS_Photo_Review_Time AS reviewTime,
       ext.BS_Photo_Review_Reason AS reviewReason,
       ph.update_time AS photoTime, ISNULL(DATALENGTH(ph.Photo), 0) AS photoBytes
  FROM dbo.ST_Person p
  LEFT JOIN dbo.ST_Department d ON d.Dept_ID = p.Dept_ID
  LEFT JOIN dbo.ST_PersonPhoto ph ON ph.Person_ID = p.Person_ID
  LEFT JOIN dbo.BS_PersonExt ext ON ext.Person_ID = p.Person_ID
 WHERE " + where + @"
    ) q
) t
WHERE t.RowNum > @offset AND t.RowNum <= @offset + @size
ORDER BY t.RowNum", parameters.ToArray());

        return Api.Ok(new { total, items });
    }

    /// <summary>GET api/card/faces/statistics：照片状态统计，一次分组查询。</summary>
    [HttpGet("statistics")]
    [RequirePermission("card:face:view")]
    public IActionResult Statistics([FromQuery] int? deptId = null, [FromQuery] bool includeChildren = true)
    {
        using var db = SqlAccess.Open();
        var parameters = new List<SqlParameter>();
        var where = BuildFilter(db, null, deptId, includeChildren, null, null, null, null, parameters);

        var stat = db.QuerySingle<CardFaceStat>($@"
SELECT COUNT(1) AS total,
       SUM(CASE WHEN DATALENGTH(ph.Photo) > 0 THEN 1 ELSE 0 END) AS withPhoto,
       SUM(CASE WHEN ISNULL(ext.BS_Face_Status, 0) = 1 THEN 1 ELSE 0 END) AS pending,
       SUM(CASE WHEN ISNULL(ext.BS_Face_Status, 0) = 2 THEN 1 ELSE 0 END) AS approved,
       SUM(CASE WHEN ISNULL(ext.BS_Face_Status, 0) = 3 THEN 1 ELSE 0 END) AS rejected,
       SUM(CASE WHEN DATALENGTH(ph.Photo) IS NULL OR DATALENGTH(ph.Photo) = 0 THEN 1 ELSE 0 END) AS noPhoto
  FROM dbo.ST_Person p
  LEFT JOIN dbo.ST_Department d ON d.Dept_ID = p.Dept_ID
  LEFT JOIN dbo.ST_PersonPhoto ph ON ph.Person_ID = p.Person_ID
  LEFT JOIN dbo.BS_PersonExt ext ON ext.Person_ID = p.Person_ID
 WHERE {where}", parameters.ToArray());

        return Api.Ok(stat ?? new CardFaceStat());
    }

    /// <summary>GET api/card/faces/{personId}：照片详情。</summary>
    [HttpGet("{personId:int}")]
    [RequirePermission("card:face:view")]
    public IActionResult Detail(int personId)
    {
        using var db = SqlAccess.Open();
        var row = db.QuerySingle<CardFaceRow>(@"
SELECT p.Person_ID AS personId, p.Person_No AS personNo, p.Person_Name AS personName,
       p.Dept_ID AS deptId, d.Dept_No AS deptNo, d.Dept_Name AS deptName,
       CASE WHEN DATALENGTH(ph.Photo) > 0 THEN 1 ELSE 0 END AS hasPhoto,
       ISNULL(ext.BS_Face_Status, 0) AS faceStatus,
       ext.BS_Photo_Review_User AS reviewUser, ext.BS_Photo_Review_Time AS reviewTime,
       ext.BS_Photo_Review_Reason AS reviewReason,
       ph.update_time AS photoTime, ISNULL(DATALENGTH(ph.Photo), 0) AS photoBytes,
       ISNULL(DATALENGTH(ph.id_photo), 0) AS idPhotoBytes
  FROM dbo.ST_Person p
  LEFT JOIN dbo.ST_Department d ON d.Dept_ID = p.Dept_ID
  LEFT JOIN dbo.ST_PersonPhoto ph ON ph.Person_ID = p.Person_ID
  LEFT JOIN dbo.BS_PersonExt ext ON ext.Person_ID = p.Person_ID
 WHERE p.Person_ID = @id", SqlSession.P("@id", personId));
        if (row == null) return Api.NotFound("人员不存在");

        row.isDel = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_Person WHERE Person_ID = @id AND ISNULL(Is_Del, 0) = 1",
            SqlSession.P("@id", personId)) > 0;
        row.historyCount = db.ScalarInt("SELECT COUNT(1) FROM dbo.BS_PhotoHistory WHERE Person_ID = @id",
            SqlSession.P("@id", personId));

        return Api.Ok(row);
    }

    /// <summary>GET api/card/faces/{personId}/image：图片流，thumb=true 返回缩略图。</summary>
    [HttpGet("{personId:int}/image")]
    [RequirePermission("card:face:view")]
    public IActionResult Image(int personId, [FromQuery] bool thumb = true, [FromQuery] string? type = null)
    {
        var wantIdPhoto = string.Equals(type, "id", StringComparison.OrdinalIgnoreCase);
        if (!thumb)
        {
            var userNo = CurrentUser;
            if (!_permission.HasPermissionAsync(userNo, "card:face:export").GetAwaiter().GetResult())
                return Api.Forbidden("缺少原图下载权限：card:face:export");
        }

        using var db = SqlAccess.Open();
        var row = db.QuerySingle<CardPhotoBlob>($@"
SELECT TOP 1 {(wantIdPhoto ? "id_photo" : "Photo")} AS photo, update_time AS updateTime
  FROM dbo.ST_PersonPhoto WHERE Person_ID = @id", SqlSession.P("@id", personId));
        var bytes = row?.photo;
        if (bytes == null || bytes.Length == 0)
        {
            // id_photo 为空时回退到 Photo，与 CS 的回填逻辑一致
            var fallback = db.QuerySingle<CardPhotoBlob>("SELECT Photo AS photo FROM dbo.ST_PersonPhoto WHERE Person_ID = @id",
                SqlSession.P("@id", personId));
            bytes = fallback?.photo;
        }
        if (bytes == null || bytes.Length == 0) return Api.NotFound("该人员暂无照片");

        if (thumb)
        {
            try
            {
                bytes = CardImage.Thumb(bytes);
            }
            catch
            {
                // 缩略图失败时返回原图，避免列表全部空白
            }
        }

        Response.Headers["Cache-Control"] = "private, max-age=300";
        return File(bytes, "image/jpeg");
    }

    /// <summary>POST api/card/faces/{personId}：上传或替换照片，压缩到 80K 以下后自动审核；通过置 2，驳回置 3并写原因。</summary>
    [HttpPost("{personId:int}")]
    [RequirePermission("card:face:upload")]
    // 超过 30KB 的 multipart 默认会溢写到系统 Temp；部分部署环境该目录不可写，这里改为内存缓冲。
    [RequestFormLimits(MemoryBufferThreshold = 6 * 1024 * 1024, MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<IActionResult> Upload(int personId, IFormFile? file)
    {
        if (file == null || file.Length == 0) return Api.BadRequest("请选择需要上传的照片");
        if (file.Length > CardImage.MaxSourceBytes) return Api.Fail(422, "42203", "照片不得超过 5MB");

        byte[] raw;
        using (var stream = new MemoryStream())
        {
            await file.CopyToAsync(stream);
            raw = stream.ToArray();
        }
        if (!CardImage.IsSupported(raw)) return Api.Fail(422, "42203", "仅支持 JPG、JPEG、PNG 格式，且需为真实图片文件");

        using var db = SqlAccess.Open();
        var person = db.QuerySingle<CardPersonBrief2>(
            "SELECT Person_ID AS personId, Person_No AS personNo, Person_Name AS personName, Is_Del AS isDel FROM dbo.ST_Person WHERE Person_ID = @id",
            SqlSession.P("@id", personId));
        if (person == null) return Api.NotFound("人员不存在");
        if (person.isDel) return Api.StateConflict("人员已注销，不能维护照片");

        byte[] jpeg;
        try
        {
            jpeg = FaceAutoReview.CompressUnder80K(raw);
        }
        catch (Exception)
        {
            return Api.Fail(422, "42203", "照片解析失败，请换一张清晰的证件照");
        }
        var (width, height) = CardImage.Measure(jpeg);
        if (jpeg.Length > FaceAutoReview.MaxPhotoBytes || width <= 0 || height <= 0)
            return Api.Fail(422, "42203", "照片压缩或解析失败，请换一张清晰的证件照");
        var approved = FaceAutoReview.Review(jpeg, out var reviewReason);
        reviewReason = approved ? string.Empty : Cut(reviewReason, 200);
        var status = approved ? 2 : 3;

        db.Begin();
        try
        {
            // 替换前保留一次历史，最多三份
            db.Exec(@"
INSERT INTO dbo.BS_PhotoHistory (Person_ID, Photo, Id_Photo, Create_User, Create_Time, Remark)
SELECT Person_ID, Photo, id_photo, @user, GETDATE(), @remark
  FROM dbo.ST_PersonPhoto WHERE Person_ID = @id AND DATALENGTH(Photo) > 0",
                SqlSession.P("@user", CurrentUser), SqlSession.P("@remark", "替换前备份"), SqlSession.P("@id", personId));

            db.Exec(@"
DELETE FROM dbo.BS_PhotoHistory
 WHERE Person_ID = @id
   AND ID_KEY NOT IN (SELECT TOP 3 ID_KEY FROM dbo.BS_PhotoHistory WHERE Person_ID = @id ORDER BY Create_Time DESC, ID_KEY DESC)",
                SqlSession.P("@id", personId));

            var affected = db.Exec(@"UPDATE dbo.ST_PersonPhoto SET Photo = @photo, id_photo = @photo, update_time = GETDATE()
                                      WHERE Person_ID = @id",
                SqlSession.P("@photo", jpeg), SqlSession.P("@id", personId));
            if (affected == 0)
            {
                db.Exec("INSERT INTO dbo.ST_PersonPhoto (Person_ID, Photo, id_photo, update_time) VALUES (@id, @photo, @photo, GETDATE())",
                    SqlSession.P("@id", personId), SqlSession.P("@photo", jpeg));
            }

            MarkStatus(db, personId, status, FaceAutoReview.AutoUser, DateTime.Now, approved ? null : reviewReason);
            db.Commit();
        }
        catch (Exception)
        {
            db.Rollback();
            throw;
        }

        _audit.LogAndSave(HttpContext, "card-face", "upload", "person-photo", personId.ToString(CultureInfo.InvariantCulture),
            null, new { sizeKb = jpeg.Length / 1024, width, height, file = file.FileName, approved, reason = approved ? null : reviewReason });

        return Api.Ok(new
        {
            personId,
            hasPhoto = true,
            faceStatus = status,
            approved,
            reason = approved ? null : reviewReason,
            sizeKb = (int)Math.Ceiling(jpeg.Length / 1024.0),
            width,
            height,
        });
    }

    /// <summary>DELETE api/card/faces/{personId}：清空照片，状态回到未采集。</summary>
    [HttpDelete("{personId:int}")]
    [RequirePermission("card:face:delete")]
    public IActionResult Delete(int personId, [FromQuery] string? reason)
    {
        using var db = SqlAccess.Open();
        var exists = db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_Person WHERE Person_ID = @id", SqlSession.P("@id", personId));
        if (exists == 0) return Api.NotFound("人员不存在");

        db.Begin();
        try
        {
            db.Exec(@"
INSERT INTO dbo.BS_PhotoHistory (Person_ID, Photo, Id_Photo, Create_User, Create_Time, Remark)
SELECT Person_ID, Photo, id_photo, @user, GETDATE(), @remark
  FROM dbo.ST_PersonPhoto WHERE Person_ID = @id AND DATALENGTH(Photo) > 0",
                SqlSession.P("@user", CurrentUser), SqlSession.P("@remark", "删除前备份"), SqlSession.P("@id", personId));

            db.Exec("UPDATE dbo.ST_PersonPhoto SET Photo = NULL, id_photo = NULL, update_time = GETDATE() WHERE Person_ID = @id",
                SqlSession.P("@id", personId));
            MarkStatus(db, personId, 0, null, null, null);
            db.Commit();
        }
        catch (Exception)
        {
            db.Rollback();
            throw;
        }

        _audit.LogAndSave(HttpContext, "card-face", "delete", "person-photo", personId.ToString(CultureInfo.InvariantCulture),
            new { hasPhoto = true }, new { hasPhoto = false, reason });
        return Api.Ok(new { personId, hasPhoto = false, faceStatus = 0 });
    }

    /// <summary>POST api/card/faces/{personId}/review：审核通过或驳回。</summary>
    [HttpPost("{personId:int}/review")]
    [RequirePermission("card:face:review")]
    public IActionResult Review(int personId, [FromBody] CardFaceReviewRequest request)
    {
        if (request == null) return Api.BadRequest("请求内容不能为空");
        using var db = SqlAccess.Open();
        var current = db.QuerySingle<CardFaceRow>(@"
SELECT p.Person_ID AS personId, ISNULL(ext.BS_Face_Status, 0) AS faceStatus,
       CASE WHEN DATALENGTH(ph.Photo) > 0 THEN 1 ELSE 0 END AS hasPhoto
  FROM dbo.ST_Person p
  LEFT JOIN dbo.ST_PersonPhoto ph ON ph.Person_ID = p.Person_ID
  LEFT JOIN dbo.BS_PersonExt ext ON ext.Person_ID = p.Person_ID
 WHERE p.Person_ID = @id", SqlSession.P("@id", personId));
        if (current == null) return Api.NotFound("人员不存在");
        if (current.hasPhoto == 0) return Api.StateConflict("该人员暂无照片，不能审核");

        var status = request.Approved ? 2 : 3;
        if (!request.Approved && string.IsNullOrWhiteSpace(request.Reason))
            return Api.BadRequest("驳回时必须填写原因");

        MarkStatus(db, personId, status, CurrentUser, DateTime.Now, request.Approved ? null : Cut(request.Reason, 200));
        _audit.LogAndSave(HttpContext, "card-face", request.Approved ? "approve" : "reject", "person-photo",
            personId.ToString(CultureInfo.InvariantCulture), new { faceStatus = current.faceStatus },
            new { faceStatus = status, reason = request.Reason });
        return Api.Ok(new { personId, faceStatus = status });
    }

    /// <summary>POST api/card/faces/batch：ZIP 批量导入，文件名取人员编号或卡号。</summary>
    [HttpPost("batch")]
    [RequirePermission("card:face:upload")]
    // 同上，避免大 ZIP 读取时溢写系统 Temp；上限与 Face.ImportZipLimitMb 默认 100MB 对齐。
    [RequestFormLimits(MemoryBufferThreshold = 110 * 1024 * 1024, MultipartBodyLengthLimit = 110 * 1024 * 1024)]
    public async Task<IActionResult> Batch(IFormFile? file)
    {
        if (file == null || file.Length == 0) return Api.BadRequest("请选择 ZIP 压缩包");

        using var db = SqlAccess.Open();
        var limitMb = CardLookup.ConfigInt(db, "Face.ImportZipLimitMb", 100);
        if (file.Length > limitMb * 1024L * 1024L) return Api.Fail(422, "42204", $"压缩包不得超过 {limitMb}MB");

        byte[] zipBytes;
        using (var stream = new MemoryStream())
        {
            await file.CopyToAsync(stream);
            zipBytes = stream.ToArray();
        }

        var jobId = Convert.ToInt64(db.Scalar(@"INSERT INTO dbo.BS_ImportJob (Job_Type, File_Name, File_Hash, Mode, Status, Create_User, Create_Time, Options_Json)
OUTPUT INSERTED.Job_ID
VALUES ('PHOTO', @name, @hash, 'CREATE', 1, @user, GETDATE(), @options)",
            SqlSession.P("@name", Cut(file.FileName, 200)),
            SqlSession.P("@hash", Convert.ToHexString(SHA256.HashData(zipBytes)).ToLowerInvariant()),
            SqlSession.P("@user", CurrentUser),
            SqlSession.P("@options", "{\"type\":\"PHOTO\"}")) ?? 0L);

        var success = 0;
        var failed = 0;
        var approved = 0;
        var rejected = 0;
        var rowNo = 0;
        var errors = new List<object>();
        var autoRejected = new List<object>();

        try
        {
            using var archive = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read);
            var entries = archive.Entries.Where(e => e.Length > 0 && !e.FullName.EndsWith("/", StringComparison.Ordinal)).ToList();
            if (entries.Count > 3000) return Api.Fail(422, "42204", "压缩包内文件数量超过 3000，请分批导入");

            foreach (var entry in entries)
            {
                rowNo++;
                var fileName = Path.GetFileName(entry.FullName);
                var key = Path.GetFileNameWithoutExtension(fileName);
                if (entry.Length > CardImage.MaxSourceBytes)
                {
                    failed++;
                    RecordRow(db, jobId, rowNo, key, null, null, fileName, "ERROR", 2, "42203", "单张照片超过 5MB");
                    errors.Add(new { row = rowNo, file = fileName, message = "单张照片超过 5MB" });
                    continue;
                }

                var person = db.QuerySingle<CardPersonBrief2>(@"
SELECT TOP 1 Person_ID AS personId, Person_No AS personNo, Person_Name AS personName, Is_Del AS isDel
  FROM dbo.ST_Person
 WHERE Person_No = @key OR (Card_No <> '' AND Card_No = @key)
 ORDER BY CASE WHEN Person_No = @key THEN 0 ELSE 1 END", SqlSession.P("@key", key));
                if (person == null)
                {
                    failed++;
                    RecordRow(db, jobId, rowNo, key, null, null, fileName, "ERROR", 2, "40401", "文件名未匹配到人员编号或卡号");
                    errors.Add(new { row = rowNo, file = fileName, message = "未匹配到人员" });
                    continue;
                }
                if (person.isDel)
                {
                    failed++;
                    RecordRow(db, jobId, rowNo, key, person.personName, person.personId, fileName, "ERROR", 2, "40902", "人员已注销");
                    errors.Add(new { row = rowNo, file = fileName, message = "人员已注销" });
                    continue;
                }

                byte[] raw;
                using (var entryStream = entry.Open())
                using (var buffer = new MemoryStream())
                {
                    entryStream.CopyTo(buffer);
                    raw = buffer.ToArray();
                }

                if (!CardImage.IsSupported(raw))
                {
                    failed++;
                    RecordRow(db, jobId, rowNo, key, person.personName, person.personId, fileName, "ERROR", 2, "42203", "图片格式不支持或文件损坏");
                    errors.Add(new { row = rowNo, file = fileName, message = "图片格式不支持" });
                    continue;
                }

                try
                {
                    var jpeg = FaceAutoReview.CompressUnder80K(raw);
                    var (photoWidth, photoHeight) = CardImage.Measure(jpeg);
                    if (jpeg.Length > FaceAutoReview.MaxPhotoBytes || photoWidth <= 0 || photoHeight <= 0)
                        throw new InvalidDataException("照片压缩或解析失败");
                    var autoApproved = FaceAutoReview.Review(jpeg, out var reviewReason);
                    var reason = autoApproved ? string.Empty : Cut(reviewReason, 200);
                    var status = autoApproved ? 2 : 3;

                    db.Exec(@"UPDATE dbo.ST_PersonPhoto SET Photo = @photo, id_photo = @photo, update_time = GETDATE() WHERE Person_ID = @id",
                        SqlSession.P("@photo", jpeg), SqlSession.P("@id", person.personId));
                    if (db.ScalarInt("SELECT COUNT(1) FROM dbo.ST_PersonPhoto WHERE Person_ID = @id", SqlSession.P("@id", person.personId)) == 0)
                    {
                        db.Exec("INSERT INTO dbo.ST_PersonPhoto (Person_ID, Photo, id_photo, update_time) VALUES (@id, @photo, @photo, GETDATE())",
                            SqlSession.P("@id", person.personId), SqlSession.P("@photo", jpeg));
                    }
                    MarkStatus(db, person.personId, status, FaceAutoReview.AutoUser, DateTime.Now, autoApproved ? null : reason);
                    success++;
                    if (autoApproved) approved++;
                    else
                    {
                        rejected++;
                        AddRejected(autoRejected, rowNo, fileName, person.personNo, reason);
                    }
                    RecordRow(db, jobId, rowNo, person.personNo, person.personName, person.personId, fileName,
                        autoApproved ? "AUTO_APPROVE" : "AUTO_REJECT", 1, null, autoApproved ? null : reason);
                }
                catch (Exception ex)
                {
                    failed++;
                    RecordRow(db, jobId, rowNo, key, person.personName, person.personId, fileName, "ERROR", 2, "42203", ex.Message);
                    errors.Add(new { row = rowNo, file = fileName, message = "照片处理失败" });
                }
            }
        }
        catch (InvalidDataException)
        {
            return Api.Fail(422, "42204", "压缩包损坏或不是有效的 ZIP 文件");
        }

        db.Exec(@"UPDATE dbo.BS_ImportJob SET Total_Rows = @total, Success_Rows = @success, Fail_Rows = @fail,
                         Status = CASE WHEN @fail = 0 THEN 4 ELSE 5 END, Finish_Time = GETDATE()
                   WHERE Job_ID = @id",
            SqlSession.P("@total", rowNo), SqlSession.P("@success", success), SqlSession.P("@fail", failed),
            SqlSession.P("@id", jobId));

        _audit.LogAndSave(HttpContext, "card-face", "batch", "import-job", jobId.ToString(CultureInfo.InvariantCulture),
            null, new { total = rowNo, success, failed, approved, rejected });
        return Api.Ok(new
        {
            jobId,
            total = rowNo,
            success,
            failed,
            approved,
            rejected,
            errors = errors.Take(100).ToList(),
            autoRejected = autoRejected.Take(100).ToList(),
        });
    }

    /* ---------------- 内部方法 ---------------- */

    private static void AddRejected(List<object> target, int rowNo, string fileName, string? personNo, string reason)
    {
        if (target.Count >= 100) return;
        target.Add(new { row = rowNo, file = fileName, personNo, reason });
    }

    private static string Cut(string? value, int length)
    {
        var text = CardRules.Trim(value);
        return text.Length <= length ? text : text.Substring(0, length);
    }

    private static void MarkStatus(SqlSession db, int personId, int status, string? reviewUser, DateTime? reviewTime, string? reason)
    {
        db.Exec(@"
IF NOT EXISTS (SELECT 1 FROM dbo.BS_PersonExt WHERE Person_ID = @id)
    INSERT INTO dbo.BS_PersonExt (Person_ID, BS_Source, BS_Face_Status, BS_Photo_Review_User, BS_Photo_Review_Time, BS_Photo_Review_Reason, BS_Update_User, BS_Update_Time)
    VALUES (@id, 'BS', @status, @user, @time, @reason, @operator, GETDATE())
ELSE
    UPDATE dbo.BS_PersonExt
       SET BS_Face_Status = @status,
           BS_Photo_Review_User = CASE WHEN @user IS NULL THEN BS_Photo_Review_User ELSE @user END,
           BS_Photo_Review_Time = CASE WHEN @time IS NULL THEN BS_Photo_Review_Time ELSE @time END,
           BS_Photo_Review_Reason = @reason,
           BS_Update_User = @operator,
           BS_Update_Time = GETDATE()
     WHERE Person_ID = @id",
            SqlSession.P("@id", personId), SqlSession.P("@status", status),
            SqlSession.P("@user", reviewUser), SqlSession.P("@time", reviewTime),
            SqlSession.P("@reason", reason),
            SqlSession.P("@operator", reviewUser ?? "BS"));
    }

    private static void RecordRow(SqlSession db, long jobId, int rowNo, string? personNo, string? personName, int? personId,
        string fileName, string action, byte status, string? errorCode, string? errorMessage)
    {
        db.Exec(@"
INSERT INTO dbo.BS_ImportRow (Job_ID, Row_No, Person_No, Person_Name, Action, Status, Error_Code, Error_Message, Person_ID, Raw_Json)
VALUES (@job, @row, @no, @name, @action, @status, @code, @message, @personId, @raw)",
            SqlSession.P("@job", jobId), SqlSession.P("@row", rowNo),
            SqlSession.P("@no", Cut(personNo, 20)), SqlSession.P("@name", Cut(personName, 20)),
            SqlSession.P("@action", action), SqlSession.P("@status", status),
            SqlSession.P("@code", errorCode), SqlSession.P("@message", Cut(errorMessage, 500)),
            SqlSession.P("@personId", personId), SqlSession.P("@raw", Cut(fileName, 400)));
    }

    private static string BuildFilter(
        SqlSession db,
        string? keyword,
        int? deptId,
        bool includeChildren,
        int? faceStatus,
        bool? hasPhoto,
        string? updateFrom,
        string? updateTo,
        List<SqlParameter> parameters)
    {
        var where = new System.Text.StringBuilder("ISNULL(p.Is_Del, 0) = 0");

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            where.Append(" AND (p.Person_No LIKE @key OR p.Person_Name LIKE @key OR p.Card_No LIKE @key)");
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

        if (faceStatus.HasValue)
        {
            where.Append(" AND ISNULL(ext.BS_Face_Status, 0) = @faceStatus");
            parameters.Add(SqlSession.P("@faceStatus", faceStatus.Value));
        }

        if (hasPhoto.HasValue)
        {
            where.Append(hasPhoto.Value ? " AND DATALENGTH(ph.Photo) > 0" : " AND ISNULL(DATALENGTH(ph.Photo), 0) = 0");
        }

        var from = CardRules.ParseDate(updateFrom);
        if (from.HasValue)
        {
            where.Append(" AND ph.update_time >= @from");
            parameters.Add(SqlSession.P("@from", from.Value));
        }
        var to = CardRules.ParseDate(updateTo);
        if (to.HasValue)
        {
            where.Append(" AND ph.update_time < @to");
            parameters.Add(SqlSession.P("@to", to.Value.AddDays(1)));
        }

        return where.ToString();
    }
}

public class CardFaceRow
{
    public int personId { get; set; }
    public string? personNo { get; set; }
    public string? personName { get; set; }
    public int? deptId { get; set; }
    public string? deptNo { get; set; }
    public string? deptName { get; set; }
    public int hasPhoto { get; set; }
    public byte faceStatus { get; set; }
    public string? reviewUser { get; set; }
    public DateTime? reviewTime { get; set; }
    public string? reviewReason { get; set; }
    public DateTime? photoTime { get; set; }
    public int photoBytes { get; set; }
    public int idPhotoBytes { get; set; }
    public bool isDel { get; set; }
    public int historyCount { get; set; }
}

public class CardFaceStat
{
    public int total { get; set; }
    public int withPhoto { get; set; }
    public int pending { get; set; }
    public int approved { get; set; }
    public int rejected { get; set; }
    public int noPhoto { get; set; }
}

public class CardFaceReviewRequest
{
    public bool Approved { get; set; }
    public string? Reason { get; set; }
}

public class CardPhotoBlob
{
    public byte[]? photo { get; set; }
    public DateTime? updateTime { get; set; }
}

public class CardPersonBrief2
{
    public int personId { get; set; }
    public string? personNo { get; set; }
    public string? personName { get; set; }
    public bool isDel { get; set; }
}
