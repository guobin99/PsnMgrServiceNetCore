using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PsnMgrService.Models;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.MicroApi;

/// <summary>
/// 意见反馈（对应 Net 版 MobileFeedbackController）：
/// 工单主表 WX_Feedback + 图片子表 WX_FeedbackImage；提交成功后写 WX_Message 回执。
/// </summary>
[ApiController]
[Authorize]
[Route("api/m/feedback")]
public class MobileFeedbackController : ControllerBase
{
    private const int MaxImageCount = 3;
    private const int MaxImageBytes = 2 * 1024 * 1024;        // 单张上限 2MB
    private const int MaxTotalImageBytes = 5 * 1024 * 1024;   // 合计上限 5MB
    private const int MinContentLength = 10;

    private readonly AppDbContext _db;

    public MobileFeedbackController(AppDbContext db)
    {
        _db = db;
    }

    private int Pid => int.TryParse(User.FindFirst("PersonId")?.Value, out var v) ? v : 0;

    // ================= 提交反馈 =================

    /// <summary>
    /// 提交意见反馈：body = { feedType, content, contact, images: [dataURL...], scene }
    /// 返回 { success, ticketNo, msg }，工单号由服务端生成（前端不得自行编造）。
    /// </summary>
    [HttpPost("submit")]
    public IActionResult Submit([FromBody] FeedbackSubmitReq req)
    {
        if (req == null) return Ok(new { success = false, msg = "参数错误" });

        string feedType = Trim(req.feedType);
        string content = Trim(req.content);
        string contact = Trim(req.contact);

        if (feedType.Length == 0) return Ok(new { success = false, msg = "请选择反馈类型" });
        if (content.Length < MinContentLength)
            return Ok(new { success = false, msg = string.Format("问题描述至少{0}个字", MinContentLength) });
        if (contact.Length == 0) return Ok(new { success = false, msg = "请填写联系方式" });

        // 与表结构对齐，超长直接截断（避免 nvarchar 溢出）
        if (feedType.Length > 20) feedType = feedType.Substring(0, 20);
        if (content.Length > 1000) content = content.Substring(0, 1000);
        if (contact.Length > 100) contact = contact.Substring(0, 100);

        // 图片解码 + 体积校验
        var images = new List<byte[]>();
        var imageTypes = new List<string>();
        if (req.images != null && req.images.Count > 0)
        {
            if (req.images.Count > MaxImageCount)
                return Ok(new { success = false, msg = string.Format("最多上传{0}张图片", MaxImageCount) });

            long total = 0;
            foreach (var item in req.images)
            {
                if (string.IsNullOrEmpty(item)) continue;
                byte[] raw;
                try { raw = DecodeDataUrl(item); }
                catch { return Ok(new { success = false, msg = "图片格式不正确，无法解析" }); }

                if (raw.Length == 0) continue;
                if (raw.Length > MaxImageBytes) return Ok(new { success = false, msg = "单张图片不能超过2MB" });
                total += raw.Length;
                if (total > MaxTotalImageBytes) return Ok(new { success = false, msg = "图片总大小不能超过5MB" });
                images.Add(raw);
                imageTypes.Add(ContentTypeOf(item));
            }
        }

        // 提交人信息快照：后台按工号/姓名检索更方便，也避免人员信息变更后对不上
        string? personNo = null, personName = null, cardNo = null;
        var person = _db.ST_People.AsNoTracking().FirstOrDefault(p => p.Person_ID == Pid);
        if (person != null)
        {
            personNo = person.Person_No;
            personName = person.Person_Name;
            cardNo = person.Card_No;
        }

        string source = Trim(req.scene).Length > 0 ? Trim(req.scene) : "h5";
        if (source.Length > 10) source = source.Substring(0, 10);

        string ticketNo = "";
        using (var cn = new SqlConnection(AppConfig.ConnectionString))
        {
            cn.Open();
            using var tx = cn.BeginTransaction();
            try
            {
                // 工单号唯一：同一秒并发时可能撞号，撞了重试
                for (int i = 0; i < 3; i++)
                {
                    string candidate = NewTicketNo();
                    using var q = new SqlCommand("SELECT COUNT(1) FROM WX_Feedback WHERE Ticket_No = @p0", cn, tx);
                    q.Parameters.AddWithValue("@p0", candidate);
                    if (Convert.ToInt32(q.ExecuteScalar() ?? 0) == 0) { ticketNo = candidate; break; }
                }
                if (ticketNo.Length == 0)
                {
                    tx.Rollback();
                    return Ok(new { success = false, msg = "工单号生成失败，请稍后重试" });
                }

                using (var ins = new SqlCommand(@"
INSERT INTO WX_Feedback
    (Ticket_No, Person_ID, Person_No, Person_Name, Card_No, Feed_Type, Content, Contact, Image_Count, Status, Source, Create_Time)
VALUES
    (@ticketNo, @personId, @personNo, @personName, @cardNo, @feedType, @content, @contact, @imgCount, 0, @source, GETDATE())", cn, tx))
                {
                    ins.Parameters.AddWithValue("@ticketNo", ticketNo);
                    ins.Parameters.AddWithValue("@personId", Pid);
                    ins.Parameters.AddWithValue("@personNo", DbNull(personNo));
                    ins.Parameters.AddWithValue("@personName", DbNull(personName));
                    ins.Parameters.AddWithValue("@cardNo", DbNull(cardNo));
                    ins.Parameters.AddWithValue("@feedType", feedType);
                    ins.Parameters.AddWithValue("@content", content);
                    ins.Parameters.AddWithValue("@contact", contact);
                    ins.Parameters.AddWithValue("@imgCount", images.Count);
                    ins.Parameters.AddWithValue("@source", source);
                    ins.ExecuteNonQuery();
                }

                for (int i = 0; i < images.Count; i++)
                {
                    using var im = new SqlCommand(@"
INSERT INTO WX_FeedbackImage (Ticket_No, Seq, Content_Type, Photo, Create_Time)
VALUES (@ticketNo, @seq, @ctype, @photo, GETDATE())", cn, tx);
                    im.Parameters.AddWithValue("@ticketNo", ticketNo);
                    im.Parameters.AddWithValue("@seq", i + 1);
                    im.Parameters.AddWithValue("@ctype", imageTypes[i]);
                    im.Parameters.AddWithValue("@photo", images[i]);
                    im.ExecuteNonQuery();
                }

                // 消息中心回执：让「处理结果将通过消息中心通知您」这句话成立
                using (var msg = new SqlCommand(@"
INSERT INTO WX_Message (Person_ID, Msg_Type, Title, Content, Is_Read, Create_Time)
VALUES (@pid, 'system', @title, @content, 0, GETDATE())", cn, tx))
                {
                    msg.Parameters.AddWithValue("@pid", Pid);
                    msg.Parameters.AddWithValue("@title", "意见反馈已提交");
                    msg.Parameters.AddWithValue("@content", string.Format("您的反馈（工单号 {0}）已受理，处理结果将通过本消息中心通知您。", ticketNo));
                    msg.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { /* 事务已回滚 */ }
                return Ok(new { success = false, msg = "提交失败：" + ex.Message });
            }
        }

        return Ok(new { success = true, ticketNo, msg = "反馈已提交" });
    }

    // ================= 我的反馈 =================

    /// <summary>我的反馈列表（按提交时间倒序，含处理状态与回复内容）</summary>
    [HttpGet("list")]
    public IActionResult List(int page = 1, int size = 20)
    {
        if (page < 1) page = 1;
        if (size < 1 || size > 50) size = 20;
        int start = (page - 1) * size + 1;
        int end = page * size;

        var rows = _db.Database.SqlQueryRaw<FeedbackRow>(@"
SELECT * FROM (
    SELECT ID_KEY, Ticket_No, Feed_Type, Content, Contact, Image_Count, CAST(Status AS int) AS Status,
           Reply_Content, Reply_Time, Create_Time,
           ROW_NUMBER() OVER (ORDER BY Create_Time DESC) AS RowNum
    FROM WX_Feedback
    WHERE Person_ID = @p0
) T
WHERE T.RowNum BETWEEN @p1 AND @p2
ORDER BY T.Create_Time DESC",
            new SqlParameter("@p0", Pid),
            new SqlParameter("@p1", start),
            new SqlParameter("@p2", end)).ToList();

        return Ok(rows.Select(x => new
        {
            id = x.ID_KEY.ToString(),
            ticketNo = x.Ticket_No ?? "",
            feedType = x.Feed_Type ?? "",
            content = x.Content ?? "",
            contact = x.Contact ?? "",
            imageCount = x.Image_Count,
            status = x.Status,
            statusText = StatusText(x.Status),
            reply = x.Reply_Content ?? "",
            replyTime = x.Reply_Time.HasValue ? x.Reply_Time.Value.ToString("yyyy-MM-dd HH:mm") : "",
            time = x.Create_Time.ToString("yyyy-MM-dd HH:mm")
        }).ToList());
    }

    // ================= 内部辅助 =================

    /// <summary>工单号：GD + yyyyMMddHHmmss + 3位随机数（19 位，落在 varchar(30) 内）</summary>
    private static string NewTicketNo()
        => "GD" + DateTime.Now.ToString("yyyyMMddHHmmss") + Random.Shared.Next(100, 1000);

    public static string StatusText(int status) => status switch
    {
        1 => "处理中",
        2 => "已回复",
        3 => "已关闭",
        _ => "待处理"
    };

    private static string Trim(string? s) => (s ?? "").Trim();

    private static object DbNull(object? v) => v ?? (object)DBNull.Value;

    private static byte[] DecodeDataUrl(string dataUrl)
    {
        int idx = dataUrl.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
        string base64 = idx >= 0 ? dataUrl.Substring(idx + 7) : dataUrl;
        return Convert.FromBase64String(base64);
    }

    private static string ContentTypeOf(string dataUrl)
    {
        if (dataUrl.StartsWith("data:image/png", StringComparison.OrdinalIgnoreCase)) return "image/png";
        if (dataUrl.StartsWith("data:image/webp", StringComparison.OrdinalIgnoreCase)) return "image/webp";
        if (dataUrl.StartsWith("data:image/gif", StringComparison.OrdinalIgnoreCase)) return "image/gif";
        return "image/jpeg";
    }

    public class FeedbackRow
    {
        public long ID_KEY { get; set; }
        public string? Ticket_No { get; set; }
        public string? Feed_Type { get; set; }
        public string? Content { get; set; }
        public string? Contact { get; set; }
        public int Image_Count { get; set; }
        public int Status { get; set; }
        public string? Reply_Content { get; set; }
        public DateTime? Reply_Time { get; set; }
        public DateTime Create_Time { get; set; }
    }
}

public class FeedbackSubmitReq
{
    public string? feedType { get; set; }
    public string? content { get; set; }
    public string? contact { get; set; }
    public List<string>? images { get; set; }
    public string? scene { get; set; }
}
