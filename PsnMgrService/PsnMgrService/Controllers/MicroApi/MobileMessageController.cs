using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PsnMgrService.Models;

namespace PsnMgrService.Controllers.MicroApi;

/// <summary>消息中心（WX_Message）：列表 / 单条已读 / 全部已读。
/// 与 PsnMgrService(Net) 的 MobileMessageController 等价（路由 api/m/message/*）。</summary>
[ApiController]
[Authorize]
[Route("api/m/message")]
public class MobileMessageController : ControllerBase
{
    private readonly AppDbContext _db;
    public MobileMessageController(AppDbContext db) => _db = db;

    private int Pid => int.TryParse(User.FindFirst("PersonId")?.Value, out var v) ? v : 0;

    [HttpGet("list")]
    public async Task<IActionResult> List(int page = 1, int size = 50)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 50;

        int start = (page - 1) * size + 1;
        int end = page * size;

        const string sql = @"
SELECT ID_KEY, Msg_Type, Title, Content, Is_Read, Create_Time
FROM (
    SELECT ID_KEY, Msg_Type, Title, Content, Is_Read, Create_Time,
           ROW_NUMBER() OVER (ORDER BY Create_Time DESC) AS RowNum
    FROM WX_Message
    WHERE Person_ID = @p0
) AS T
WHERE T.RowNum BETWEEN @p1 AND @p2
ORDER BY T.Create_Time DESC";

        var rows = await _db.Database.SqlQueryRaw<MessageRow>(sql,
            new SqlParameter("@p0", Pid),
            new SqlParameter("@p1", start),
            new SqlParameter("@p2", end)).ToListAsync();

        return Ok(rows.Select(x => new
        {
            id = x.ID_KEY.ToString(),
            type = x.Msg_Type ?? "system",
            title = x.Title ?? "",
            content = x.Content ?? "",
            time = x.Create_Time.ToString("yyyy-MM-dd HH:mm"),
            read = x.Is_Read
        }).ToList());
    }

    [HttpPost("read")]
    public async Task<IActionResult> Read([FromBody] MessageReadReq req)
    {
        if (req == null || !long.TryParse(req.id, out var id)) return BadRequest("参数错误");
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE WX_Message SET Is_Read = 1 WHERE ID_KEY = @p0 AND Person_ID = @p1", id, Pid);
        return Ok(new { success = true });
    }

    [HttpPost("readall")]
    public async Task<IActionResult> ReadAll()
    {
        await _db.Database.ExecuteSqlRawAsync("UPDATE WX_Message SET Is_Read = 1 WHERE Person_ID = @p0", Pid);
        return Ok(new { success = true });
    }

    public class MessageRow
    {
        public long ID_KEY { get; set; }
        public string? Msg_Type { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
        public bool Is_Read { get; set; }
        public DateTime Create_Time { get; set; }
    }
}

public class MessageReadReq
{
    public string? id { get; set; }
}
