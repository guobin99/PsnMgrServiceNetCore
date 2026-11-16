using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PsnMgrService.Models;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers;

/// <summary>
/// 管理端订餐查询（Pc 后台「订餐系统」用，对应 Net 版 Admin_OrderController）。
/// 按 ST_SysParam.Param_Code='DC.Solution' 分流：
///   =1 订餐扣款 → DC_OrderData + DC_OrderDataSub
///   =0 消费扣款 → SF_OrderData + SF_OrderDataSub
/// 关联 ST_Person（订餐人姓名/工号）、SF_Repast（餐别名）、XF_WareInfo（菜品名）。
/// </summary>
[ApiController]
[Authorize]
[Route("api/Admin_Order")]
public class Admin_OrderController : ControllerBase
{
    private readonly AppDbContext _db;

    public Admin_OrderController(AppDbContext db)
    {
        _db = db;
    }

    // GET: api/Admin_Order?bgn=&end=&personName=&personNo=&repastNo=&dataType=
    [HttpGet]
    public async Task<IActionResult> GetOrders(
        string bgn = "", string end = "",
        string personName = "", string personNo = "", string repastNo = "",
        string dataType = "")
    {
        string mode = OrderMode();

        // 餐别名
        var repasts = _db.SF_Repasts.AsNoTracking().ToDictionary(r => r.Repast_No, r => r.Repast_Name ?? "");
        // 订餐人：Person_ID → 工号 / 姓名
        var people = _db.ST_People.AsNoTracking().Select(p => new { p.Person_ID, p.Person_No, p.Person_Name }).ToList();
        var personNos = people.ToDictionary(p => p.Person_ID, p => p.Person_No ?? "");
        var personNames = people.ToDictionary(p => p.Person_ID, p => p.Person_Name ?? "");
        // 菜品名：Ware_ID → Ware_Name
        var wareNames = _db.XF_WareInfos.AsNoTracking().ToDictionary(w => w.Ware_ID, w => w.Ware_Name ?? "");

        // 时间区间按下单时间闭区间处理（起止当天都包含）；bgn/end 均为空表示不限
        DateTime? dtBgn = ParseDay(bgn);
        DateTime? dtEnd = ParseDay(end);

        var list = new List<object>();

        if (mode == "1")
        {
            var q = _db.DC_OrderData.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(repastNo)) q = q.Where(o => o.Repast_No == repastNo);
            if (!string.IsNullOrEmpty(dataType) && dataType == "1") q = q.Where(o => o.Data_Type == 1);
            if (dtBgn.HasValue) q = q.Where(o => o.Order_Time >= dtBgn.Value);
            if (dtEnd.HasValue) q = q.Where(o => o.Order_Time < dtEnd.Value.AddDays(1));

            var orders = q.OrderByDescending(o => o.Order_Time).ToList();
            var orderNos = orders.Select(o => o.Order_No ?? "").ToList();
            // Contains → EF 翻成 OPENJSON/CTE（2016+），2008 必须用 Dapper 的 IN
            var subs = (await Db.QueryAsync<AdminOrderSubRow>(
                    "SELECT Order_No, Ware_ID, Ware_Price, Ware_Copies FROM DC_OrderDataSub WHERE Order_No IN @orderNos",
                    new { orderNos }))
                .GroupBy(s => s.Order_No ?? "")
                .ToDictionary(g => g.Key, g => g.Select(s => Dish(s.Ware_ID, s.Ware_Price, s.Ware_Copies, wareNames)).ToList());

            foreach (var o in orders)
            {
                if (!MatchPerson(o.Person_ID, personName, personNo, personNames, personNos)) continue;

                if (!subs.TryGetValue(o.Order_No ?? "", out var dishes)) dishes = new List<object>();
                list.Add(new
                {
                    orderNo = o.Order_No,
                    orderDate = o.Order_Date,
                    orderTime = o.Order_Time,
                    personId = o.Person_ID,
                    personNo = NameOf(o.Person_ID, personNos),
                    personName = NameOf(o.Person_ID, personNames),
                    repastNo = o.Repast_No,
                    repastName = RepastName(o.Repast_No, repasts),
                    amount = o.Order_Fund ?? 0,
                    beforeFund = o.Order_BeforeFund ?? 0,
                    isWareOrder = o.Is_WareOrder,
                    copies = o.copies,
                    mode = "订餐扣款",
                    modeValue = mode,
                    // DC_OrderData.Data_Type：0 已订餐，1 已取餐
                    dataType = o.Data_Type,
                    status = o.Data_Type == 1 ? "已取餐" : "已订餐",
                    consumeTime = o.Consume_Time,
                    dishes
                });
            }
        }
        else
        {
            var q = _db.SF_OrderData.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(repastNo)) q = q.Where(o => o.Repast_No == repastNo);
            if (dtBgn.HasValue) q = q.Where(o => o.Order_Time >= dtBgn.Value);
            if (dtEnd.HasValue) q = q.Where(o => o.Order_Time < dtEnd.Value.AddDays(1));

            var orders = q.OrderByDescending(o => o.Order_Time).ToList();
            var orderNos = orders.Select(o => o.Order_No ?? "").ToList();
            var subs = (await Db.QueryAsync<AdminOrderSubRow>(
                    "SELECT Order_No, Ware_ID, Ware_Price, Ware_Copies FROM SF_OrderDataSub WHERE Order_No IN @orderNos",
                    new { orderNos }))
                .GroupBy(s => s.Order_No ?? "")
                .ToDictionary(g => g.Key, g => g.Select(s => Dish(s.Ware_ID, s.Ware_Price, s.Ware_Copies, wareNames)).ToList());

            foreach (var o in orders)
            {
                if (!MatchPerson(o.Person_ID ?? 0, personName, personNo, personNames, personNos)) continue;

                if (!subs.TryGetValue(o.Order_No ?? "", out var dishes)) dishes = new List<object>();
                list.Add(new
                {
                    orderNo = o.Order_No,
                    orderDate = o.Order_date,
                    orderTime = o.Order_Time,
                    personId = o.Person_ID ?? 0,
                    personNo = NameOf(o.Person_ID ?? 0, personNos),
                    personName = NameOf(o.Person_ID ?? 0, personNames),
                    repastNo = o.Repast_No,
                    repastName = RepastName(o.Repast_No, repasts),
                    amount = o.Order_Fund ?? 0,
                    beforeFund = o.Order_BeforeFund ?? 0,
                    isWareOrder = o.Is_WareOrder,
                    copies = o.copies,
                    mode = "消费扣款",
                    modeValue = mode,
                    dataType = 0,
                    // 消费扣款模式：订餐时不扣款，取餐时才扣，故统一为已订餐（待取餐）
                    status = "已订餐",
                    consumeTime = (DateTime?)null,
                    dishes
                });
            }
        }

        return Ok(list);
    }

    /// <summary>
    /// 管理端撤销订餐（与移动端 /api/m/canteen/cancel 同一口径）：
    ///   =1 订餐扣款 → 退回余额并写一条 XF_Consume 退款流水（Consume_Mode=21、负数），再删订单与明细；
    ///   =0 消费扣款 → 订餐时未扣钱，仅删除 SF_OrderData，不产生任何退款。
    /// 已过 DC_Repast 截止时间的不允许撤销，确需撤销可传 force=true。
    /// </summary>
    [HttpPost("Cancel")]
    public IActionResult CancelOrder([FromBody] AdminCancelReq req)
    {
        if (req == null || string.IsNullOrEmpty(req.orderNo)) return Ok(new { success = false, msg = "订单号不能为空" });
        bool force = req.force;
        string mode = OrderMode();

        if (mode != "1")
        {
            var sf = _db.SF_OrderData.FirstOrDefault(x => x.Order_No == req.orderNo);
            if (sf == null) return Ok(new { success = false, msg = "订单不存在" });

            // 过了该餐别订餐截止时间（后厨已备餐）不允许撤销
            if (!CheckRepastCancellable(sf.Repast_No, sf.Order_date, force, out var sfTip))
                return Ok(new { success = false, msg = sfTip });

            var sfSubs = _db.SF_OrderDataSubs.Where(s => s.Order_No == sf.Order_No).ToList();
            foreach (var s in sfSubs) _db.SF_OrderDataSubs.Remove(s);

            _db.SF_OrderData.Remove(sf);
            _db.SaveChanges();
            // 消费扣款模式订餐时未扣钱，撤销只取消订单、没有任何退款发生
            return Ok(new { success = true, refunded = false, refundAmount = 0, mode, modeText = "消费扣款" });
        }

        var o = _db.DC_OrderData.FirstOrDefault(x => x.Order_No == req.orderNo);
        if (o == null) return Ok(new { success = false, msg = "订单不存在" });
        if (o.Data_Type == 1 || o.Consume_Time.HasValue) return Ok(new { success = false, msg = "已取餐，无法撤销" });

        if (!CheckRepastCancellable(o.Repast_No, o.Order_Date, force, out var dcTip))
            return Ok(new { success = false, msg = dcTip });

        int pid = o.Person_ID;
        var card = _db.ST_Cards.FirstOrDefault(x => x.Person_ID == pid);
        decimal refunded = 0;
        if (card != null && o.Order_Fund.HasValue)
        {
            decimal before = card.Card_Balance;
            card.Card_Balance = before + o.Order_Fund.Value;
            refunded = o.Order_Fund.Value;
            _db.XF_Consumes.Add(new XF_Consume
            {
                Person_ID = pid,
                System_No = "",
                Consume_Mode = 21,
                Consume_Time = DateTime.Now,
                Consume_Fund = -o.Order_Fund.Value,
                Before_Fund = before,
                Repast_No = o.Repast_No,
                MOC_No = "WXH5",
                Card_Balance = card.Card_Balance,
                Data_Type = 1,
                Remark = "管理端撤销订餐退款",
                OrderID = o.Order_No
            });
        }
        var orderSubs = _db.DC_OrderDataSubs.Where(s => s.Order_No == o.Order_No).ToList();
        foreach (var s in orderSubs) _db.DC_OrderDataSubs.Remove(s);
        _db.DC_OrderData.Remove(o);
        _db.SaveChanges();
        return Ok(new { success = true, refunded = refunded > 0, refundAmount = refunded, mode, modeText = "订餐扣款" });
    }

    // 订餐模式：ST_SysParam.Param_Code='DC.Solution'，1=订餐扣款，0=消费扣款
    private string OrderMode()
    {
        string? mode = _db.ST_SysParams.AsNoTracking()
            .Where(s => s.Param_Code == "DC.Solution")
            .Select(s => s.Param_Value).FirstOrDefault();
        return (mode ?? "0").Trim() == "1" ? "1" : "0";
    }

    /// <summary>把 "12:00" / "12:00:00" 解析为 TimeSpan；空或非法按 00:00 处理</summary>
    private static TimeSpan ParseHhMm(string? s)
    {
        if (string.IsNullOrEmpty(s)) return TimeSpan.Zero;
        if (TimeSpan.TryParse(s.Trim(), out var ts)) return ts;
        if (DateTime.TryParse(s.Trim(), out var d)) return d.TimeOfDay;
        return TimeSpan.Zero;
    }

    /// <summary>
    /// 撤销订餐的截止时间校验：DC_Repast.Order_Days（需提前预定天数）+ End_Time（截止时间），
    /// 例：Order_Days=1、End_Time=12:00 表示用餐日 D 需在 (D-1) 日 12:00 前完成订/撤销动作。
    /// </summary>
    private bool CheckRepastCancellable(string? repastNo, string? orderDate, bool force, out string tip)
    {
        tip = "";
        if (force) return true;                    // 强制撤销（管理员特批）
        var dc = _db.DC_Repasts.AsNoTracking().FirstOrDefault(x => x.Repast_No == repastNo);
        if (dc == null) return true;               // 未配置订餐规则 → 不限制
        if (!DateTime.TryParse(orderDate, out var od)) return true;  // 用餐日期不可解析 → 不拦截
        DateTime deadline = od.Date.AddDays(-dc.Order_Days).Add(ParseHhMm(dc.End_Time));
        if (DateTime.Now > deadline)
        {
            tip = string.Format("已过订餐截止时间（{0:MM-dd HH:mm}），无法撤销", deadline);
            return false;
        }
        return true;
    }

    /// <summary>解析区间起止日（yyyy-MM-dd）为当天 00:00:00；空或非法返回 null（表示不限）</summary>
    private static DateTime? ParseDay(string s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        if (!DateTime.TryParse(s, out var d)) return null;
        return d.Date;
    }

    private static bool MatchPerson(int personId, string personName, string personNo,
        Dictionary<int, string> personNames, Dictionary<int, string> personNos)
    {
        if (string.IsNullOrEmpty(personName) && string.IsNullOrEmpty(personNo)) return true;
        string name = NameOf(personId, personNames);
        string no = NameOf(personId, personNos);
        if (!string.IsNullOrEmpty(personName) && name.IndexOf(personName, StringComparison.OrdinalIgnoreCase) < 0) return false;
        if (!string.IsNullOrEmpty(personNo) && no.IndexOf(personNo, StringComparison.OrdinalIgnoreCase) < 0) return false;
        return true;
    }

    private static string NameOf(int personId, Dictionary<int, string> map)
        => map.TryGetValue(personId, out var v) ? v : "";

    private static string RepastName(string? repastNo, Dictionary<string, string> repasts)
    {
        if (repastNo != null && repasts.TryGetValue(repastNo, out var name)) return name;
        return repastNo ?? "";
    }

    private static object Dish(int wareId, decimal price, int copies, Dictionary<int, string> wareNames)
    {
        if (!wareNames.TryGetValue(wareId, out var name)) name = "";
        return new
        {
            wareId,
            wareName = name,
            warePrice = price,
            copies,
            subtotal = price * copies
        };
    }
}

public class AdminCancelReq { public string? orderNo { get; set; } public bool force { get; set; } }

public class AdminOrderSubRow
{
    public string? Order_No { get; set; }
    public int Ware_ID { get; set; }
    public decimal Ware_Price { get; set; }
    public int Ware_Copies { get; set; }
}
