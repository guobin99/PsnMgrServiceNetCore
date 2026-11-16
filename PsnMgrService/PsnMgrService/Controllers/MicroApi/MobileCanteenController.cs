using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PsnMgrService.Models;
using PsnMgrService.Services;

namespace PsnMgrService.Controllers.MicroApi;

/// <summary>
/// 移动端食堂订餐（对应 Net 版 MobileQueryController 的 canteen/* 部分，9 个端点）。
/// 按 ST_SysParam.Param_Code='DC.Solution' 分流：1=订餐扣款(DC_OrderData + XF_Consume)、0=消费扣款(SF_OrderData，取餐才扣)。
/// 订餐规则来自 DC_Repast：Can_Order / Order_Days + End_Time 截止时间；份数来自 XF_WareInWeek.Order_Copies。
/// </summary>
[ApiController]
[Authorize]
[Route("api/m/canteen")]
public class MobileCanteenController : ControllerBase
{
    private readonly AppDbContext _db;

    public MobileCanteenController(AppDbContext db)
    {
        _db = db;
    }

    private int Pid => int.TryParse(User.FindFirst("PersonId")?.Value, out var v) ? v : 0;

    // ================= 食堂 / 餐别 =================

    /// <summary>
    /// 就餐区域（菜谱页的「食堂」筛选）。
    /// XF_Area 是通用区域表（含南大门、超市等），只能返回 SF 前缀且确有菜谱的区域；
    /// 都没排菜时回退返回全部 SF 区域，避免下拉为空。
    /// </summary>
    [HttpGet("areas")]
    public IActionResult Areas()
    {
        var sfAreas = _db.XF_Areas.AsNoTracking().Where(a => a.Area_No.StartsWith("SF")).ToList();
        var withMenu = _db.XF_WareInWeeks.AsNoTracking().Select(w => w.Area_No).Distinct().ToList();

        var list = sfAreas.Where(a => withMenu.Contains(a.Area_No)).ToList();
        if (list.Count == 0) list = sfAreas;

        return Ok(list.Select(a => new { a.Area_No, a.Area_Name }).ToList());
    }

    /// <summary>
    /// 餐别列表：SF_Repast 与 DC_Repast 通过 Repast_No 关联。
    /// Can_Order=1 允许订餐；Order_Days + End_Time 为截止规则；没有 DC_Repast 记录的餐别 hasRule=false（默认可订）。
    /// </summary>
    [HttpGet("repasts")]
    public IActionResult Repasts()
    {
        DateTime now = DateTime.Now;
        var rules = new Dictionary<string, DC_Repast>();
        foreach (var d in _db.DC_Repasts.AsNoTracking().ToList())
            if (!string.IsNullOrEmpty(d.Repast_No) && !rules.ContainsKey(d.Repast_No)) rules[d.Repast_No] = d;

        var rows = _db.SF_Repasts.AsNoTracking().ToList().Select(r =>
        {
            DC_Repast? dc = null;
            bool has = !string.IsNullOrEmpty(r.Repast_No) && rules.TryGetValue(r.Repast_No, out dc);
            var rule = BuildRepastRule(has ? dc : null, now);
            return new
            {
                r.Repast_No,
                r.Repast_Name,
                r.Bgn_Time,
                r.End_Time,
                r.Price,
                hasRule = has,
                canOrder = rule.CanOrder,
                orderDays = rule.OrderDays,
                dcEndTime = rule.EndTime,
                latestOrderDate = rule.LatestOrderDate.HasValue ? rule.LatestOrderDate.Value.ToString("yyyy-MM-dd") : "",
                latestDeadlineText = rule.LatestDeadline.HasValue ? rule.LatestDeadline.Value.ToString("yyyy-MM-dd HH:mm") : "",
                deadlineTip = rule.DeadlineTip
            };
        }).ToList();
        return Ok(rows);
    }

    // ================= 菜谱 =================

    /// <summary>
    /// 每日菜品：XF_WareInWeek（Ware_Week 周一=1..周日=7）+ 餐别 + 食堂。
    /// 剩余份数 = Order_Copies − 已预定份数（只统计今天及以后、且周几匹配的订单）；Order_Copies 为空表示不限量，返回 -1。
    /// </summary>
    [HttpGet("menu")]
    public IActionResult Menu(int week, string repastNo, string areaNo, string date = "")
    {
        const string sql = @"
SELECT i.Ware_ID AS Ware_ID, i.Ware_Name AS Ware_Name, i.Ware_Price AS Ware_Price, i.Ware_Unit AS Ware_Unit,
       t.Type_Name AS Type_Name, w.Remark AS Remark, w.Order_Copies AS Order_Copies,
       CAST(CASE WHEN EXISTS(SELECT 1 FROM XF_WareInfoPhoto ph WHERE ph.Ware_ID = i.Ware_ID) THEN 1 ELSE 0 END AS bit) AS HasPhoto
FROM XF_WareInWeek w
JOIN XF_WareInfo i ON i.Ware_ID = w.Ware_ID
LEFT JOIN XF_WareType t ON t.Type_No = i.Ware_Type
WHERE w.Ware_Week = @week AND w.Repast_No = @repastNo
  AND (@areaNo = '' OR ISNULL(w.Area_No, '') = @areaNo)";

        var rows = _db.Database.SqlQueryRaw<MenuRow>(sql,
            new SqlParameter("@week", week),
            new SqlParameter("@repastNo", repastNo ?? ""),
            new SqlParameter("@areaNo", areaNo ?? "")).ToList();

        // 已预定份数（同一餐别 + 同一周几），用于扣减剩余
        var ordered = OrderedCopies(repastNo, week);

        // 餐别订餐规则（Can_Order / Order_Days / End_Time）
        var dcRule = _db.DC_Repasts.AsNoTracking().FirstOrDefault(x => x.Repast_No == repastNo);
        bool hasDate = DateTime.TryParse(date, out var od);
        bool canOrder = CheckRepastCanOrder(dcRule, hasDate ? od : DateTime.Today, DateTime.Now, out var tip);

        return Ok(rows.Select(x =>
        {
            ordered.TryGetValue(x.Ware_ID, out var used);
            int remain = x.Order_Copies.HasValue ? Math.Max(0, x.Order_Copies.Value - used) : -1;
            return new
            {
                wareId = x.Ware_ID,
                wareName = x.Ware_Name,
                price = x.Ware_Price,
                unit = x.Ware_Unit,
                typeName = x.Type_Name ?? "",
                remark = x.Remark,
                orderCopies = x.Order_Copies,
                orderedCopies = used,
                remainCopies = remain,
                canOrder,
                orderTip = tip,
                photo = x.HasPhoto ? "/api/m/canteen/photo/" + x.Ware_ID : ""   // 空则前端用默认图
            };
        }).ToList());
    }

    /// <summary>
    /// 今日菜谱（首页用）。餐别取当前时段→即将开始→第一个；食堂必须在「有菜谱的 SF 区域」里挑
    /// （直接 FirstOrDefault 会取到 MJ01 南大门 → 菜谱恒为空）。
    /// </summary>
    [HttpGet("today")]
    public IActionResult TodayMenu(string areaNo = "")
    {
        DateTime today = DateTime.Now.Date;
        int week = WeekOf(today);                                // 周一=1 .. 周日=7
        string weekText = "周" + "一二三四五六日"[week - 1];
        string now = DateTime.Now.ToString("HH:mm");

        // 餐别：优先当前时间所在餐别，其次即将开始的餐别，最后取第一个
        var repasts = _db.SF_Repasts.AsNoTracking().OrderBy(r => r.Bgn_Time).ToList();
        var repast = repasts.FirstOrDefault(r => (r.Bgn_Time ?? "").CompareTo(now) <= 0 && (r.End_Time ?? "").CompareTo(now) >= 0)
                  ?? repasts.FirstOrDefault(r => (r.Bgn_Time ?? "").CompareTo(now) > 0)
                  ?? repasts.FirstOrDefault();

        // 食堂
        XF_Area? area;
        if (!string.IsNullOrEmpty(areaNo))
        {
            area = _db.XF_Areas.AsNoTracking().FirstOrDefault(a => a.Area_No == areaNo)
                ?? _db.XF_Areas.AsNoTracking().FirstOrDefault();
        }
        else
        {
            var menuAreas = _db.XF_WareInWeeks.AsNoTracking().Select(w => w.Area_No).Distinct().ToList();
            var sfAreas = _db.XF_Areas.AsNoTracking().Where(a => a.Area_No.StartsWith("SF")).ToList();
            area = sfAreas.FirstOrDefault(a => menuAreas.Contains(a.Area_No))   // 有菜谱的食堂优先
                ?? sfAreas.FirstOrDefault()                                     // 任一食堂
                ?? _db.XF_Areas.AsNoTracking().FirstOrDefault();                  // 兜底
        }

        string rNo = repast?.Repast_No ?? "";
        string aNo = area?.Area_No ?? "";

        // 按 Ware_Week 匹配（与 Net 版一致：Ware_Date 精确匹配分支已停用）
        bool byDate = false;
        var rows = QueryToday(today, week, rNo, aNo, false);

        var ordered = OrderedCopies(rNo, week);
        var dcRule = _db.DC_Repasts.AsNoTracking().FirstOrDefault(x => x.Repast_No == rNo);
        bool canOrder = CheckRepastCanOrder(dcRule, today, DateTime.Now, out var tip);
        var rule = BuildRepastRule(dcRule, DateTime.Now);

        return Ok(new
        {
            date = today.ToString("yyyy-MM-dd"),
            week,
            weekText,
            areaNo = aNo,
            areaName = area?.Area_Name ?? "",
            repastNo = rNo,
            repastName = repast?.Repast_Name ?? "",
            timeRange = repast != null ? (repast.Bgn_Time ?? "") + "-" + (repast.End_Time ?? "") : "",
            byDate,
            canOrder,
            orderDays = rule.OrderDays,
            dcEndTime = rule.EndTime,
            latestOrderDate = rule.LatestOrderDate.HasValue ? rule.LatestOrderDate.Value.ToString("yyyy-MM-dd") : "",
            latestDeadlineText = rule.LatestDeadline.HasValue ? rule.LatestDeadline.Value.ToString("yyyy-MM-dd HH:mm") : "",
            orderTip = tip,
            dishes = rows.Select(x =>
            {
                ordered.TryGetValue(x.Ware_ID, out var used);
                return new
                {
                    wareId = x.Ware_ID,
                    name = x.Ware_Name,
                    price = x.Ware_Price,
                    unit = x.Ware_Unit ?? "",
                    typeName = x.Type_Name ?? "",
                    remark = x.Remark ?? "",
                    orderCopies = x.Order_Copies,
                    orderedCopies = used,
                    remainCopies = x.Order_Copies.HasValue ? Math.Max(0, x.Order_Copies.Value - used) : -1
                };
            }).ToList()
        });
    }

    private List<TodayMenuRow> QueryToday(DateTime today, int week, string repastNo, string areaNo, bool byDate)
    {
        const string sql = @"
SELECT i.Ware_ID AS Ware_ID, i.Ware_Name AS Ware_Name, i.Ware_Price AS Ware_Price, i.Ware_Unit AS Ware_Unit,
       t.Type_Name AS Type_Name, w.Remark AS Remark, w.Order_Copies AS Order_Copies
FROM XF_WareInWeek w
JOIN XF_WareInfo i ON i.Ware_ID = w.Ware_ID
LEFT JOIN XF_WareType t ON t.Type_No = i.Ware_Type
WHERE ((@byDate = 1 AND CAST(w.Ware_Date AS DATE) = @today)
    OR (@byDate = 0 AND w.Ware_Week = @week))
  AND ISNULL(w.Repast_No, '') = @repastNo
  AND (@areaNo = '' OR ISNULL(w.Area_No, '') = @areaNo)
ORDER BY t.Type_Name, i.Ware_Name";
        return _db.Database.SqlQueryRaw<TodayMenuRow>(sql,
            new SqlParameter("@byDate", byDate ? 1 : 0),
            new SqlParameter("@today", today),
            new SqlParameter("@week", week),
            new SqlParameter("@repastNo", repastNo ?? ""),
            new SqlParameter("@areaNo", areaNo ?? "")).ToList();
    }

    /// <summary>菜品图片（XF_WareInfoPhoto.Photo），找不到返回 404</summary>
    [HttpGet("photo/{id}")]
    public IActionResult Photo(int id)
    {
        var ph = _db.XF_WareInfoPhotos.AsNoTracking().FirstOrDefault(x => x.Ware_ID == id);
        if (ph?.Photo == null || ph.Photo.Length == 0) return NotFound();

        // 允许浏览器缓存 1 小时（与 Net 版一致）
        Response.Headers.CacheControl = "public,max-age=3600";
        return File(ph.Photo, "image/jpeg");
    }

    // ================= 订餐 =================

    /// <summary>
    /// 订餐：DC.Solution=1 订餐扣款（扣余额 + DC_OrderData/Sub + XF_Consume）；=0 消费扣款（SF_OrderData/Sub，取餐才扣）。
    /// 提交前校验：① Can_Order=0 拒绝；② 过 Order_Days/End_Time 截止时间拒绝；③ 逐菜品校验剩余份数。
    /// 所有写库操作在同一次 SaveChanges 内（EF 自动包事务），保证订单/明细/扣款原子性。
    /// </summary>
    [HttpPost("order")]
    public IActionResult Order([FromBody] OrderReq req)
    {
        if (req == null || req.items == null || req.items.Count == 0) return BadRequest("未选择菜品");

        DateTime now = DateTime.Now;
        if (!DateTime.TryParse(req.orderDate, out var orderDate))
            return Ok(new { success = false, msg = "用餐日期不正确" });
        orderDate = orderDate.Date;

        // ---- ① ② 餐别订餐规则 ----
        var dcRule = _db.DC_Repasts.AsNoTracking().FirstOrDefault(x => x.Repast_No == req.repastNo);
        if (!CheckRepastCanOrder(dcRule, orderDate, now, out var ruleTip))
            return Ok(new { success = false, msg = ruleTip });

        // ---- ③ 剩余份数校验 ----
        int week = WeekOf(orderDate);
        var ordered = OrderedCopies(req.repastNo, week);
        var want = new Dictionary<int, int>();
        foreach (var it in req.items)
        {
            if (it.wareId <= 0 || it.copies <= 0) continue;
            want.TryGetValue(it.wareId, out var cur);
            want[it.wareId] = cur + it.copies;
        }
        foreach (var kv in want)
        {
            if (!TryGetWareLimit(kv.Key, req.repastNo, week, req.areaNo, out int limit)) continue; // 未配置份数=不限量
            ordered.TryGetValue(kv.Key, out var used);
            if (used + kv.Value > limit)
            {
                var wareName = _db.XF_WareInfos.Where(x => x.Ware_ID == kv.Key).Select(x => x.Ware_Name).FirstOrDefault() ?? "";
                return Ok(new
                {
                    success = false,
                    msg = string.Format("「{0}」剩余份数不足：仅剩 {1} 份，本次需要 {2} 份", wareName, Math.Max(0, limit - used), kv.Value)
                });
            }
        }

        var p = _db.ST_People.FirstOrDefault(x => x.Person_ID == Pid);
        var card = _db.ST_Cards.FirstOrDefault(x => x.Person_ID == Pid);
        decimal total = req.items.Sum(it => it.price * it.copies);
        string mode = OrderMode();
        string orderNo = (mode == "1" ? "DC" : "SF") + now.ToString("yyyyMMddHHmmss") + Pid;
        // Is_WareOrder=1 按菜品订餐（明细落 Sub 表）；=0 按餐别订套餐（只记 copies 份数）
        bool isWareOrder = req.items.Any(it => it.wareId > 0);
        int? copies = isWareOrder ? null : Math.Max(1, req.items.Sum(it => it.copies));

        if (mode == "1")
        {
            if (card == null || card.Card_Balance < total) return Ok(new { success = false, msg = "卡余额不足" });
            decimal before = card.Card_Balance;
            card.Card_Balance = before - total;
            card.Update_Time = now;

            _db.DC_OrderData.Add(new DC_OrderDatum
            {
                Person_ID = Pid,
                Order_Date = req.orderDate!,
                Order_Time = now,
                Repast_No = req.repastNo!,
                Moc_No_DC = "WXH5",
                Data_Type = 0,
                Order_Fund = total,
                Order_BeforeFund = before,
                Is_WareOrder = isWareOrder,
                Order_No = orderNo,
                copies = copies
            });
            foreach (var it in req.items)
            {
                if (it.wareId <= 0) continue;
                _db.DC_OrderDataSubs.Add(new DC_OrderDataSub
                {
                    Ware_ID = it.wareId,
                    Order_No = orderNo,
                    Ware_Copies = it.copies,
                    Ware_Price = it.price,
                    Ware_BeforeFund = card.Card_Balance + total,
                    Ware_Fund = it.price * it.copies
                });
            }
            _db.XF_Consumes.Add(new XF_Consume
            {
                Person_ID = Pid,
                System_No = req.areaNo ?? "",
                Consume_Mode = 20,
                Consume_Time = now,
                Consume_Fund = total,
                Before_Fund = before,
                Repast_No = req.repastNo,
                MOC_No = "WXH5",
                COM_No = orderNo,          // COM_No 为订单号
                Card_Balance = card.Card_Balance,
                Data_Type = 0,
                Remark = "微信订餐扣款",
                OrderID = orderNo
            });
        }
        else
        {
            _db.SF_OrderData.Add(new SF_OrderDatum
            {
                Person_ID = Pid,
                Card_No = p?.Card_No ?? "",
                Order_date = req.orderDate!,
                Repast_No = req.repastNo!,
                Moc_No = "WXH5",
                Note = "微信订餐",
                Order_Time = now,
                Order_Fund = total,
                Order_BeforeFund = card != null ? card.Card_Balance : 0,
                Is_WareOrder = isWareOrder,
                Order_No = orderNo,
                copies = copies
            });
            foreach (var it in req.items)
            {
                if (it.wareId <= 0) continue;
                _db.SF_OrderDataSubs.Add(new SF_OrderDataSub
                {
                    Ware_ID = it.wareId,
                    Order_No = orderNo,
                    Ware_Copies = it.copies,
                    Ware_Price = it.price,
                    Ware_BeforeFund = card != null ? card.Card_Balance : 0,
                    Ware_Fund = it.price * it.copies
                });
            }
        }

        _db.SaveChanges();
        MobileMessageService.Push(Pid, "order", "订餐成功",
            string.Format("{0} 订餐成功，共 {1:0.00} 元（{2}，单号 {3}）",
                req.orderDate, total, mode == "1" ? "订餐扣款" : "消费扣款", orderNo));
        return Ok(new { success = true, orderNo, total, mode });
    }

    /// <summary>
    /// 订餐记录报表（按 DC.Solution 分流）。
    /// 时间口径：byMealDate=false 按下单时间 Order_Time；true 按用餐日期 Order_Date（字符串比较）。
    /// 订餐存的是未来日期，默认必须按下单时间过滤，否则刚下的单查不到。
    /// </summary>
    [HttpGet("myorders")]
    public async Task<IActionResult> MyOrders(string bgn = "", string end = "", bool byMealDate = false)
    {
        string mode = OrderMode();
        var repasts = _db.SF_Repasts.AsNoTracking().ToDictionary(r => r.Repast_No, r => r.Repast_Name ?? "");
        var wareNames = _db.XF_WareInfos.AsNoTracking().ToDictionary(w => w.Ware_ID, w => w.Ware_Name ?? "");
        var list = new List<object>();

        DateTime? dtBgn = ParseDay(bgn);
        DateTime? dtEnd = ParseDay(end);
        DateTime dtBgnVal = dtBgn ?? DateTime.MinValue;
        DateTime dtEndExcl = dtEnd.HasValue ? dtEnd.Value.AddDays(1) : DateTime.MaxValue;
        // 用餐日期口径（字符串 yyyy-MM-dd，字典序比较即日期序）
        string? mealBgn = dtBgn?.ToString("yyyy-MM-dd");
        string? mealEnd = dtEnd?.ToString("yyyy-MM-dd");

        if (mode == "1")
        {
            var q = _db.DC_OrderData.AsNoTracking().Where(o => o.Person_ID == Pid);
            if (byMealDate)
            {
                if (mealBgn != null) q = q.Where(o => string.Compare(o.Order_Date, mealBgn) >= 0);
                if (mealEnd != null) q = q.Where(o => string.Compare(o.Order_Date, mealEnd) <= 0);
            }
            else
            {
                if (dtBgn.HasValue) q = q.Where(o => o.Order_Time >= dtBgnVal);
                if (dtEnd.HasValue) q = q.Where(o => o.Order_Time < dtEndExcl);
            }
            var orders = q.OrderByDescending(o => o.Order_Time).ToList();
            // 先把订单号提取为 string 集合，再用 Contains 让 EF 翻译成 SQL IN
            var orderNos = orders.Select(o => o.Order_No).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
            // orderNos.Contains(...) 会被 EF 翻成 OPENJSON/CTE（2016+），改 Dapper 的 IN @orderNos
            var subs = (await Db.QueryAsync<OrderSubRow>(
                    "SELECT Order_No, Ware_ID, Ware_Price, Ware_Copies FROM DC_OrderDataSub WHERE Order_No IN @orderNos",
                    new { orderNos }))
                .GroupBy(s => s.Order_No ?? "")
                .ToDictionary(g => g.Key, g => g.Select(s => Dish(s.Ware_ID, s.Ware_Price, s.Ware_Copies, wareNames)).ToList());

            foreach (var o in orders)
            {
                if (!subs.TryGetValue(o.Order_No ?? "", out var dishes)) dishes = new List<object>();
                list.Add(new
                {
                    orderNo = o.Order_No,
                    orderDate = o.Order_Date,
                    orderTime = o.Order_Time,
                    repastNo = o.Repast_No,
                    repastName = RepastName(o.Repast_No, repasts),
                    amount = o.Order_Fund ?? 0,
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
            var q = _db.SF_OrderData.AsNoTracking().Where(o => o.Person_ID == Pid);
            if (byMealDate)
            {
                if (mealBgn != null) q = q.Where(o => string.Compare(o.Order_date, mealBgn) >= 0);
                if (mealEnd != null) q = q.Where(o => string.Compare(o.Order_date, mealEnd) <= 0);
            }
            else
            {
                if (dtBgn.HasValue) q = q.Where(o => o.Order_Time >= dtBgnVal);
                if (dtEnd.HasValue) q = q.Where(o => o.Order_Time < dtEndExcl);
            }
            var orders = q.OrderByDescending(o => o.Order_Time).ToList();
            var orderNos = orders.Select(o => o.Order_No).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
            var subs = (await Db.QueryAsync<OrderSubRow>(
                    "SELECT Order_No, Ware_ID, Ware_Price, Ware_Copies FROM SF_OrderDataSub WHERE Order_No IN @orderNos",
                    new { orderNos }))
                .GroupBy(s => s.Order_No ?? "")
                .ToDictionary(g => g.Key, g => g.Select(s => Dish(s.Ware_ID, s.Ware_Price, s.Ware_Copies, wareNames)).ToList());

            foreach (var o in orders)
            {
                if (!subs.TryGetValue(o.Order_No ?? "", out var dishes)) dishes = new List<object>();
                list.Add(new
                {
                    orderNo = o.Order_No,
                    orderDate = o.Order_date,
                    orderTime = o.Order_Time,
                    repastNo = o.Repast_No,
                    repastName = RepastName(o.Repast_No, repasts),
                    amount = o.Order_Fund ?? 0,
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
    /// 当前订餐模式：mode=1 订餐扣款（下单即扣）、0 消费扣款（取餐才扣）。
    /// 前端不得自行假设扣款时机，一律以此接口返回为准。
    /// </summary>
    [HttpGet("ordermode")]
    public IActionResult OrderModeInfo()
    {
        string mode = OrderMode();
        bool payAtOrder = mode == "1";
        return Ok(new
        {
            mode,
            modeText = payAtOrder ? "订餐扣款" : "消费扣款",
            payAtOrder,
            payAtPickup = !payAtOrder,
            tip = payAtOrder
                ? "订餐扣款：提交订单即从一卡通余额扣款，退订将原路退回余额。"
                : "消费扣款：提交订单不扣款，取餐时才从一卡通余额扣款。"
        });
    }

    /// <summary>
    /// 退订：DC.Solution=1 → 退 DC_OrderData 并原路退回余额（写 XF_Consume 退款流水）；
    /// =0 → 订餐时未扣款，直接删除 SF_OrderData 记录，不产生任何退款。
    /// </summary>
    [HttpPost("cancel")]
    public IActionResult CancelOrder([FromBody] CancelOrderReq req)
    {
        if (req == null || string.IsNullOrEmpty(req.orderNo)) return Ok(new { success = false, msg = "订单号不能为空" });
        string mode = OrderMode();

        if (mode != "1")
        {
            var sf = _db.SF_OrderData.FirstOrDefault(x => x.Person_ID == Pid && x.Order_No == req.orderNo);
            if (sf == null) return Ok(new { success = false, msg = "订单不存在" });

            // 撤销同样受 DC_Repast 截止时间约束：过了截止时间（已备餐）不允许撤销
            if (DateTime.TryParse(sf.Order_date, out var sfDate))
            {
                if (!CheckRepastCancellable(sf.Repast_No, sfDate.Date, out var sfTip))
                    return Ok(new { success = false, msg = sfTip });
            }

            var sfSubs = _db.SF_OrderDataSubs.Where(s => s.Order_No == sf.Order_No).ToList();
            foreach (var s in sfSubs) _db.SF_OrderDataSubs.Remove(s);
            _db.SF_OrderData.Remove(sf);
            _db.SaveChanges();
            // 消费扣款模式订餐时未扣钱，退订只取消订单、没有任何退款发生
            return Ok(new { success = true, refunded = 0, refundAmount = 0, mode, modeText = "消费扣款" });
        }

        var o = _db.DC_OrderData.FirstOrDefault(x => x.Person_ID == Pid && x.Order_No == req.orderNo);
        if (o == null) return Ok(new { success = false, msg = "订单不存在" });
        if (o.Data_Type == 1 || o.Consume_Time.HasValue) return Ok(new { success = false, msg = "已取餐，无法取消" });

        if (DateTime.TryParse(o.Order_Date, out var dcDate))
        {
            if (!CheckRepastCancellable(o.Repast_No, dcDate.Date, out var dcTip))
                return Ok(new { success = false, msg = dcTip });
        }

        var card = _db.ST_Cards.FirstOrDefault(x => x.Person_ID == Pid);
        decimal refunded = 0;
        if (card != null && o.Order_Fund.HasValue)
        {
            decimal before = card.Card_Balance;
            card.Card_Balance = before + o.Order_Fund.Value;
            refunded = o.Order_Fund.Value;
            _db.XF_Consumes.Add(new XF_Consume
            {
                Person_ID = Pid,
                System_No = "",
                Consume_Mode = 21,
                Consume_Time = DateTime.Now,
                Consume_Fund = -o.Order_Fund.Value,
                Before_Fund = before,
                Repast_No = o.Repast_No,
                MOC_No = "WXH5",
                Card_Balance = card.Card_Balance,
                Data_Type = 1,
                Remark = "微信取消订餐退款",
                OrderID = o.Order_No
            });
        }
        var orderSubs = _db.DC_OrderDataSubs.Where(s => s.Order_No == o.Order_No).ToList();
        foreach (var s in orderSubs) _db.DC_OrderDataSubs.Remove(s);
        _db.DC_OrderData.Remove(o);
        _db.SaveChanges();
        return Ok(new { success = true, refunded = refunded > 0, refundAmount = refunded, mode, modeText = "订餐扣款" });
    }

    // ================= 内部：订餐规则与份数 =================

    /// <summary>订餐模式：ST_SysParam.Param_Code='DC.Solution'，1=订餐扣款，0=消费扣款</summary>
    private string OrderMode()
    {
        string? mode = _db.ST_SysParams.AsNoTracking()
            .Where(s => s.Param_Code == "DC.Solution")
            .Select(s => s.Param_Value).FirstOrDefault();
        return (mode ?? "0").Trim() == "1" ? "1" : "0";
    }

    /// <summary>餐别订餐规则（由 DC_Repast 解析而来）</summary>
    private sealed class RepastRule
    {
        public bool CanOrder = true;
        public int OrderDays;
        public string EndTime = "";
        public DateTime? LatestOrderDate;   // 最晚可订的用餐日期（含当天）；null 表示已无可订日期
        public DateTime? LatestDeadline;    // 该最晚日期对应的订餐截止时刻
        public string DeadlineTip = "";
    }

    /// <summary>把 "12:00" / "12:00:00" 解析为 TimeSpan；空或非法按 00:00 处理</summary>
    private static TimeSpan ParseHhMm(string? s)
    {
        if (string.IsNullOrEmpty(s)) return TimeSpan.Zero;
        if (TimeSpan.TryParse(s.Trim(), out var ts)) return ts;
        if (DateTime.TryParse(s.Trim(), out var d)) return d.TimeOfDay;
        return TimeSpan.Zero;
    }

    /// <summary>日期对应的星期序号：周一=1 .. 周日=7（与 XF_WareInWeek.Ware_Week 一致）</summary>
    private static int WeekOf(DateTime date) => ((int)date.DayOfWeek + 6) % 7 + 1;

    /// <summary>用餐日 D 的订餐截止时刻 = (D - Order_Days) 日的 End_Time</summary>
    private static DateTime DeadlineOf(DateTime orderDate, int orderDays, string? endTime)
        => orderDate.Date.AddDays(-orderDays).Add(ParseHhMm(endTime));

    /// <summary>根据 DC_Repast 与当前时间解析订餐规则与最晚可订日期</summary>
    private static RepastRule BuildRepastRule(DC_Repast? dc, DateTime now)
    {
        var r = new RepastRule();
        if (dc == null) return r;   // 未配置规则 → 默认可订、不限截止时间

        r.CanOrder = dc.Can_Order;
        r.OrderDays = dc.Order_Days;
        r.EndTime = dc.End_Time ?? "";

        DateTime today = now.Date;
        DateTime todayDeadline = today.Add(ParseHhMm(r.EndTime));
        // 今天尚未到今天的截止时间 → 可订到 今天+Order_Days；否则顺延一天
        int add = dc.Order_Days + (now > todayDeadline ? 1 : 0);
        r.LatestOrderDate = today.AddDays(add);
        r.LatestDeadline = r.LatestOrderDate.Value.AddDays(-dc.Order_Days).Add(ParseHhMm(r.EndTime));

        if (dc.Order_Days > 0)
            r.DeadlineTip = string.Format("需提前 {0} 天预定，{1:MM-dd HH:mm} 前截止，最晚可订 {2:yyyy-MM-dd}",
                dc.Order_Days, r.LatestDeadline.Value, r.LatestOrderDate.Value);
        else
            r.DeadlineTip = string.Format("当天 {0:hh\\:mm} 前截止，最晚可订 {1:yyyy-MM-dd}",
                ParseHhMm(r.EndTime), r.LatestOrderDate.Value);
        return r;
    }

    /// <summary>
    /// 校验某餐别在某用餐日是否仍可订（仅看规则，不含份数）：
    /// Can_Order=0 直接拒绝；已过 Order_Days/End_Time 截止时间也拒绝。
    /// </summary>
    private static bool CheckRepastCanOrder(DC_Repast? dc, DateTime orderDate, DateTime now, out string tip)
    {
        tip = "";
        if (dc == null) return true;              // 未配置规则 → 不拦截
        if (!dc.Can_Order)
        {
            tip = "该餐别未开放订餐";
            return false;
        }
        DateTime deadline = DeadlineOf(orderDate, dc.Order_Days, dc.End_Time);
        if (now > deadline)
        {
            tip = string.Format("已过订餐截止时间（{0:MM-dd HH:mm}），无法预订 {1:yyyy-MM-dd} 的餐", deadline, orderDate);
            return false;
        }
        tip = string.Format("需提前 {0} 天预定，{1:MM-dd HH:mm} 前截止", dc.Order_Days, deadline);
        return true;
    }

    /// <summary>撤销订餐校验：同样受 DC_Repast 截止时间约束（过了截止时间说明后厨已备餐，不允许撤销）。</summary>
    private bool CheckRepastCancellable(string? repastNo, DateTime orderDate, out string tip)
    {
        tip = "";
        var dc = _db.DC_Repasts.AsNoTracking().FirstOrDefault(x => x.Repast_No == repastNo);
        if (dc == null) return true;
        DateTime deadline = DeadlineOf(orderDate, dc.Order_Days, dc.End_Time);
        if (DateTime.Now > deadline)
        {
            tip = string.Format("已过订餐截止时间（{0:MM-dd HH:mm}），无法取消", deadline);
            return false;
        }
        return true;
    }

    /// <summary>
    /// 已预定份数：按 Ware_ID 汇总「今天及以后用餐」的订单份数，只保留与 week 匹配的记录。
    /// DC.Solution=1 取 DC_OrderDataSub + DC_OrderData；=0 取 SF_OrderDataSub + SF_OrderData。
    /// 退订是物理删除，故删除后份数自动释放。
    /// </summary>
    private Dictionary<int, int> OrderedCopies(string? repastNo, int week)
    {
        var res = new Dictionary<int, int>();
        if (string.IsNullOrEmpty(repastNo)) return res;
        string today = DateTime.Now.ToString("yyyy-MM-dd");

        List<WareCopiesRow> rows;
        if (OrderMode() == "1")
        {
            rows = _db.Database.SqlQueryRaw<WareCopiesRow>(@"
SELECT s.Ware_ID AS Ware_ID, ISNULL(m.Order_Date,'') AS Order_Date, SUM(s.Ware_Copies) AS Copies
FROM DC_OrderDataSub s
JOIN DC_OrderData m ON m.Order_No = s.Order_No
WHERE ISNULL(m.Repast_No,'') = @repastNo AND m.Order_Date >= @today
GROUP BY s.Ware_ID, ISNULL(m.Order_Date,'')",
                new SqlParameter("@repastNo", repastNo),
                new SqlParameter("@today", today)).ToList();
        }
        else
        {
            rows = _db.Database.SqlQueryRaw<WareCopiesRow>(@"
SELECT s.Ware_ID AS Ware_ID, ISNULL(m.Order_date,'') AS Order_Date, SUM(s.Ware_Copies) AS Copies
FROM SF_OrderDataSub s
JOIN SF_OrderData m ON m.Order_No = s.Order_No
WHERE ISNULL(m.Repast_No,'') = @repastNo AND m.Order_date >= @today
GROUP BY s.Ware_ID, ISNULL(m.Order_date,'')",
                new SqlParameter("@repastNo", repastNo),
                new SqlParameter("@today", today)).ToList();
        }

        foreach (var r in rows)
        {
            if (!DateTime.TryParse(r.Order_Date, out var d)) continue;   // 用餐日期不可解析的不参与扣减
            if (WeekOf(d) != week) continue;                            // 菜谱按周几匹配，只扣同周几的预定
            res.TryGetValue(r.Ware_ID, out var cur);
            res[r.Ware_ID] = cur + (r.Copies ?? 0);
        }
        return res;
    }

    /// <summary>
    /// 取菜谱行 XF_WareInWeek.Order_Copies 作为该菜品在该餐别/周几下的可订总量。
    /// 返回 false 表示未配置（不限量）；areaNo 为空时不按食堂过滤。
    /// </summary>
    private bool TryGetWareLimit(int wareId, string? repastNo, int week, string? areaNo, out int limit)
    {
        const string sql = @"
SELECT ISNULL(w.Order_Copies, 0) AS Order_Copies
FROM XF_WareInWeek w
WHERE w.Ware_ID = @wareId AND w.Ware_Week = @week AND ISNULL(w.Repast_No,'') = @repastNo
  AND (@areaNo = '' OR ISNULL(w.Area_No,'') = @areaNo)";
        var rows = _db.Database.SqlQueryRaw<WareLimitRow>(sql,
            new SqlParameter("@wareId", wareId),
            new SqlParameter("@week", week),
            new SqlParameter("@repastNo", repastNo ?? ""),
            new SqlParameter("@areaNo", areaNo ?? "")).ToList();

        if (rows.Count == 0)
        {
            limit = 0;
            return false;      // 菜谱未配置该菜品 → 不做份数限制（保持原有宽松行为）
        }
        // 多行（多食堂）时取总量最大值，避免因任一食堂行缺值而误判为 0
        bool hasValue = rows.Any(x => x.Order_Copies.HasValue);
        limit = rows.Where(x => x.Order_Copies.HasValue).Select(x => x.Order_Copies!.Value).DefaultIfEmpty(0).Max();
        return hasValue;
    }

    /// <summary>解析区间起止日（yyyy-MM-dd）为当天 00:00:00；空或非法返回 null（表示不限）</summary>
    private static DateTime? ParseDay(string s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        if (!DateTime.TryParse(s, out var d)) return null;
        return d.Date;
    }

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

/// <summary>SqlQuery 载体：Ware_ID + 用餐日期 + 份数</summary>
public class WareCopiesRow
{
    public int Ware_ID { get; set; }
    public string? Order_Date { get; set; }
    public int? Copies { get; set; }
}

/// <summary>XF_WareInWeek.Order_Copies 载体（只用于份数上限查询）</summary>
public class WareLimitRow
{
    public int? Order_Copies { get; set; }
}

public class TodayMenuRow
{
    public int Ware_ID { get; set; }
    public string? Ware_Name { get; set; }
    public decimal Ware_Price { get; set; }
    public string? Ware_Unit { get; set; }
    public string? Type_Name { get; set; }
    public string? Remark { get; set; }
    public int? Order_Copies { get; set; }
}

public class MenuRow
{
    public int Ware_ID { get; set; }
    public string? Ware_Name { get; set; }
    public decimal Ware_Price { get; set; }
    public string? Ware_Unit { get; set; }
    public string? Type_Name { get; set; }
    public string? Remark { get; set; }
    public int? Order_Copies { get; set; }
    public bool HasPhoto { get; set; }
}

public class OrderItemReq
{
    public int wareId { get; set; }
    public int copies { get; set; }
    public decimal price { get; set; }
}

public class OrderReq
{
    public string? repastNo { get; set; }
    public string? areaNo { get; set; }
    public string? orderDate { get; set; }
    public List<OrderItemReq>? items { get; set; }
}

public class CancelOrderReq { public string? orderNo { get; set; } }

public class OrderSubRow
{
    public string? Order_No { get; set; }
    public int Ware_ID { get; set; }
    public decimal Ware_Price { get; set; }
    public int Ware_Copies { get; set; }
}
