using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PsnMgrService.Controllers.MicroApi;
using PsnMgrService.Models;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers;

/// <summary>
/// 管理端意见反馈（Pc 后台，对应 Net 版 Admin_FeedbackController）：列表查询 / 回复处理 / 查看反馈图片。
/// 数据来源 WX_Feedback + WX_FeedbackImage（提交入口是移动端 /api/m/feedback/submit）。
/// </summary>
[ApiController]
[Authorize]
[Route("api/Admin_Feedback")]
public class Admin_FeedbackController : ControllerBase
{
    private readonly AppDbContext _db;

    public Admin_FeedbackController(AppDbContext db)
    {
        _db = db;
    }

    // GET: api/Admin_Feedback?status=&bgn=&end=&keyword=&page=1&size=20
    [HttpGet]
    public IActionResult GetList(
        string status = "", string bgn = "", string end = "",
        string keyword = "", int page = 1, int size = 20)
    {
        if (page < 1) page = 1;
        if (size < 1 || size > 200) size = 20;

        string where = " WHERE 1=1 ";
        var ps = new List<SqlParameter>();

        if (!string.IsNullOrEmpty(status) && int.TryParse(status, out var st))
        {
            where += " AND Status = @status ";
            ps.Add(new SqlParameter("@status", st));
        }
        if (DateTime.TryParse(bgn, out var dt))
        {
            where += " AND Create_Time >= @bgn ";
            ps.Add(new SqlParameter("@bgn", dt.Date));
        }
        if (DateTime.TryParse(end, out dt))
        {
            where += " AND Create_Time < @end ";
            ps.Add(new SqlParameter("@end", dt.Date.AddDays(1)));
        }
        if (!string.IsNullOrEmpty(keyword))
        {
            where += @" AND (Ticket_No LIKE @kw OR Person_No LIKE @kw OR Person_Name LIKE @kw
                       OR Content LIKE @kw OR Contact LIKE @kw) ";
            ps.Add(new SqlParameter("@kw", "%" + keyword.Trim() + "%"));
        }

        int total = _db.Database
            .SqlQueryRaw<int>("SELECT COUNT(1) AS Value FROM WX_Feedback " + where, ps.ToArray())
            .FirstOrDefault();

        int start = (page - 1) * size + 1;
        int endRow = page * size;

        var sql = @"
SELECT * FROM (
    SELECT ID_KEY, Ticket_No, Person_ID, Person_No, Person_Name, Card_No, Feed_Type, Content, Contact,
           Image_Count, CAST(Status AS int) AS Status, Reply_Content, Reply_User, Reply_Time, Source, Create_Time,
           ROW_NUMBER() OVER (ORDER BY Create_Time DESC) AS RowNum
    FROM WX_Feedback " + where + @"
) T
WHERE T.RowNum BETWEEN @start AND @endRow
ORDER BY T.Create_Time DESC";

        var ps2 = new List<SqlParameter>(ps)
        {
            new SqlParameter("@start", start),
            new SqlParameter("@endRow", endRow)
        };

        var rows = _db.Database.SqlQueryRaw<FeedbackAdminRow>(sql, ps2.ToArray()).ToList();

        return Ok(new
        {
            total,
            page,
            size,
            rows = rows.Select(x => new
            {
                id = x.ID_KEY.ToString(),
                ticketNo = x.Ticket_No ?? "",
                personId = x.Person_ID,
                personNo = x.Person_No ?? "",
                personName = x.Person_Name ?? "",
                cardNo = x.Card_No ?? "",
                feedType = x.Feed_Type ?? "",
                content = x.Content ?? "",
                contact = x.Contact ?? "",
                imageCount = x.Image_Count,
                status = x.Status,
                statusText = MobileFeedbackController.StatusText(x.Status),
                reply = x.Reply_Content ?? "",
                replyUser = x.Reply_User ?? "",
                replyTime = x.Reply_Time.HasValue ? x.Reply_Time.Value.ToString("yyyy-MM-dd HH:mm") : "",
                source = x.Source ?? "",
                time = x.Create_Time.ToString("yyyy-MM-dd HH:mm")
            }).ToList()
        });
    }

    // GET: api/Admin_Feedback/image?ticketNo=GD20261105170000123&seq=1
    [HttpGet("image")]
    public IActionResult GetImage(string ticketNo, int seq = 1)
    {
        var rows = _db.Database.SqlQueryRaw<FeedbackImageRow>(@"
SELECT Content_Type, Photo FROM WX_FeedbackImage
WHERE Ticket_No = @p0 AND Seq = @p1",
            new SqlParameter("@p0", ticketNo ?? ""),
            new SqlParameter("@p1", seq)).ToList();

        if (rows.Count == 0 || rows[0].Photo == null || rows[0].Photo!.Length == 0)
            return NotFound();

        string ct = string.IsNullOrEmpty(rows[0].Content_Type) ? "image/jpeg" : rows[0].Content_Type!;
        return File(rows[0].Photo!, ct);
    }

    // POST: api/Admin_Feedback/reply  body = { ticketNo, reply, status }
    /// <summary>
    /// 处理反馈：写回复内容与状态（1=处理中 2=已回复 3=已关闭），
    /// 并给提交人写一条消息中心通知。status 为空时按「已回复」处理。
    /// </summary>
    [HttpPost("reply")]
    public IActionResult Reply([FromBody] FeedbackReplyReq req)
    {
        if (req == null || string.IsNullOrEmpty(req.ticketNo))
            return Ok(new { success = false, msg = "工单号不能为空" });

        string reply = (req.reply ?? "").Trim();
        if (reply.Length > 1000) reply = reply.Substring(0, 1000);
        int status = req.status > 0 ? req.status : 2;
        if (status < 1 || status > 3) status = 2;

        var rows = _db.Database.SqlQueryRaw<FeedbackAdminRow>(@"
SELECT ID_KEY, Ticket_No, Person_ID, Person_No, Person_Name, Card_No, Feed_Type, Content, Contact,
       Image_Count, CAST(Status AS int) AS Status, Reply_Content, Reply_User, Reply_Time, Source, Create_Time,
       CAST(0 AS bigint) AS RowNum
FROM WX_Feedback WHERE Ticket_No = @p0", new SqlParameter("@p0", req.ticketNo)).ToList();
        if (rows.Count == 0) return Ok(new { success = false, msg = "工单不存在" });

        var row = rows[0];
        string replyUser = string.IsNullOrEmpty(req.replyUser) ? "管理员" : req.replyUser!.Trim();

        _db.Database.ExecuteSqlRaw(@"
UPDATE WX_Feedback
SET Status = @status, Reply_Content = @reply, Reply_User = @user, Reply_Time = GETDATE()
WHERE Ticket_No = @ticketNo",
            new SqlParameter("@status", status),
            new SqlParameter("@reply", DbNull(reply)),
            new SqlParameter("@user", replyUser),
            new SqlParameter("@ticketNo", req.ticketNo));

        // 通知提交人（没有回复内容时只更新状态、不打扰用户）
        if (reply.Length > 0)
        {
            string title = status == 3 ? "意见反馈已关闭" : "意见反馈已回复";
            string content = string.Format("您提交的反馈（工单号 {0}）处理结果：{1}", req.ticketNo, reply);
            _db.Database.ExecuteSqlRaw(@"
INSERT INTO WX_Message (Person_ID, Msg_Type, Title, Content, Is_Read, Create_Time)
VALUES (@pid, 'system', @title, @content, 0, GETDATE())",
                new SqlParameter("@pid", row.Person_ID),
                new SqlParameter("@title", title),
                new SqlParameter("@content", content));
        }

        return Ok(new { success = true, status, statusText = MobileFeedbackController.StatusText(status) });
    }

    private static object DbNull(object? v) => v ?? (object)DBNull.Value;

    public class FeedbackAdminRow
    {
        public long ID_KEY { get; set; }
        public string? Ticket_No { get; set; }
        public int Person_ID { get; set; }
        public string? Person_No { get; set; }
        public string? Person_Name { get; set; }
        public string? Card_No { get; set; }
        public string? Feed_Type { get; set; }
        public string? Content { get; set; }
        public string? Contact { get; set; }
        public int Image_Count { get; set; }
        public int Status { get; set; }
        public string? Reply_Content { get; set; }
        public string? Reply_User { get; set; }
        public DateTime? Reply_Time { get; set; }
        public string? Source { get; set; }
        public DateTime Create_Time { get; set; }
        public long RowNum { get; set; }
    }

    public class FeedbackImageRow
    {
        public string? Content_Type { get; set; }
        public byte[]? Photo { get; set; }
    }
}

public class FeedbackReplyReq
{
    public string? ticketNo { get; set; }
    public string? reply { get; set; }
    public int status { get; set; }
    public string? replyUser { get; set; }
}
