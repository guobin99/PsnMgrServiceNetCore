using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PsnMgrService.Models;
using PsnMgrService.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PsnMgrService.Controllers.MicroApi;

/// <summary>
/// 访客管理（对应 Net 版 MobileVisitorController）。
/// 数据职责（按厂商表语义）：
///   VisitorUser  —— 访客档案，证件号为主键，存姓名 / 手机 / 单位 / 证件类别 / 照片 / OpenID
///   VisitorApply —— 申请单，只存证件号引用 + 拜访时段 + 审批、签到、签离时间；
///                   Visitor_Number 是预约码（不是姓名），Empower_Card_No 是真实临时卡号。
/// 凭证：临时卡（刷闸机）+ 二维码（前台核验，token 后端签名，/api/m/visitor/verify 校验）。
/// 状态：pending → approved → checkedin → completed；超过 End_Date 未签到为 expired。
/// </summary>
[ApiController]
[Authorize]
[Route("api/m/visitor")]
public class MobileVisitorController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<MobileVisitorController> _logger;

    public MobileVisitorController(AppDbContext db, ILogger<MobileVisitorController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private int Pid => int.TryParse(User.FindFirst("PersonId")?.Value, out var v) ? v : 0;

    // ================= 列表 =================

    [HttpGet("list")]
    public async Task<IActionResult> List(string scope = "", string idCard = "")
    {
        string myOpenId = CurrentOpenId();
        // 走 Dapper：EF Core 9 会对 SqlQueryRaw 做 CTE 包装，SQL Server 2008(compat 90) 直接语法报错
        List<VisitorRow> rows;
        if (scope == "visitor" && !string.IsNullOrEmpty(idCard))
        {
            rows = (await Db.QueryAsync<VisitorRow>(@"
SELECT v.ID_KEY, v.Apply_No, v.Person_ID, v.Person_Mobile, v.Visitor_Number, v.Start_Date, v.End_Date,
       v.VisitOrNot, v.Subject_Matter, v.Carry_items, v.CreateTime, v.VisitorID_number, v.car_number,
       v.Visitor_Company, v.visitor_type, v.Empower_Card_No, v.Approved_User, v.Approved_Time,
       v.Approved_Reason, v.check_intime, v.check_outtime, v.update_time, v.visitors_number, v.OpenID,
       u.Visitor_Name, u.PhoneNumber,
       CASE WHEN u.VisitorPhoto IS NULL THEN 0 ELSE 1 END AS HasPhoto
FROM VisitorApply v
LEFT JOIN VisitorUser u ON u.VisitorID_number = v.VisitorID_number
WHERE v.VisitorID_number = @p0
ORDER BY v.CreateTime DESC", new { p0 = idCard.Trim() })).ToList();
        }
        else
        {
            // 我的访客：我是被访人  OR  这条是我提交的（代录场景，靠 openid 快照认人）
            rows = (await Db.QueryAsync<VisitorRow>(@"
SELECT v.ID_KEY, v.Apply_No, v.Person_ID, v.Person_Mobile, v.Visitor_Number, v.Start_Date, v.End_Date,
       v.VisitOrNot, v.Subject_Matter, v.Carry_items, v.CreateTime, v.VisitorID_number, v.car_number,
       v.Visitor_Company, v.visitor_type, v.Empower_Card_No, v.Approved_User, v.Approved_Time,
       v.Approved_Reason, v.check_intime, v.check_outtime, v.update_time, v.visitors_number, v.OpenID,
       u.Visitor_Name, u.PhoneNumber,
       CASE WHEN u.VisitorPhoto IS NULL THEN 0 ELSE 1 END AS HasPhoto
FROM VisitorApply v
LEFT JOIN VisitorUser u ON u.VisitorID_number = v.VisitorID_number
WHERE (v.Person_ID = @p0 OR (@p1 <> '' AND v.OpenID = @p1))
ORDER BY v.CreateTime DESC", new { p0 = Pid, p1 = myOpenId })).ToList();
        }

        var now = DateTime.Now;

        // 被访人姓名与部门：批量查，避免逐条 N+1
        var pids = rows.Where(x => x.Person_ID.HasValue).Select(x => x.Person_ID!.Value).Distinct().ToList();
        // 被访人姓名/部门也用 Dapper（EF Core 的 Contains → OPENJSON/CTE 在 2008 上不可用）
        var personRows = (await Db.QueryAsync<PersonNameRow>(
            "SELECT Person_ID, Person_Name, Dept_ID FROM ST_Person WHERE Person_ID IN @pids",
            new { pids })).ToList();
        var persons = personRows.ToDictionary(x => x.Person_ID, x => x.Person_Name ?? "");
        var deptMap = (await Db.QueryAsync<DeptRow>("SELECT Dept_ID, Dept_Name FROM ST_Department", null))
            .GroupBy(d => d.Dept_ID).ToDictionary(g => g.Key, g => g.First().Dept_Name ?? "");
        var personDept = personRows.ToDictionary(x => x.Person_ID, x => x.Dept_ID);

        // 惰性回收：已通过、没签到、已过拜访结束时间的记录，把临时卡收回
        // （否则访客没来、签离又要求已签到，这张卡会永远占着）
        foreach (var expired in rows.Where(x => x.VisitOrNot == 1 && !x.check_intime.HasValue
                                                && x.check_outtime == null
                                                && x.End_Date.HasValue && x.End_Date.Value < now))
        {
            try { VisitorCardPool.Release(expired.Apply_No); } catch { /* 回收失败不影响列表 */ }
        }

        return Ok(rows.Select(v => new
        {
            id = v.ID_KEY.ToString(),
            applyNo = v.Apply_No ?? "",
            // 姓名/手机以 VisitorUser 档案为准，不再用预约码字段冒充姓名
            visitorName = v.Visitor_Name ?? "",
            visitorPhone = MaskPhone(v.PhoneNumber ?? ""),
            idCard = MaskIdCard(v.VisitorID_number ?? ""),
            company = v.Visitor_Company ?? "",
            // 访客记录里更该看“找谁”，而不是只看部门
            respondentName = v.Person_ID.HasValue && persons.ContainsKey(v.Person_ID.Value) ? persons[v.Person_ID.Value] : "",
            department = DeptNameOf(v.Person_ID, deptMap, personDept),
            reason = v.Subject_Matter ?? "",
            visitDate = (v.Start_Date ?? v.update_time).ToString("yyyy-MM-dd"),
            visitTime = TimeRange(v.Start_Date, v.End_Date),
            companions = Math.Max(0, v.visitors_number - 1),
            plate = v.car_number ?? "",
            status = StatusOf(v, now),
            passCode = string.IsNullOrEmpty(v.Empower_Card_No) ? (v.Visitor_Number ?? "") : v.Empower_Card_No,
            cardNo = v.Empower_Card_No ?? "",
            visitToken = BuildTokenIfValid(v, now),
            checkinTime = v.check_intime.HasValue ? v.check_intime.Value.ToString("yyyy-MM-dd HH:mm") : "",
            signOutTime = v.check_outtime.HasValue ? v.check_outtime.Value.ToString("yyyy-MM-dd HH:mm") : "",
            rejectReason = v.VisitOrNot == 2 ? (v.Approved_Reason ?? "") : "",
            applyTime = (v.CreateTime ?? v.update_time).ToString("yyyy-MM-dd HH:mm"),
            carryItems = v.Carry_items ?? "",
            visitorType = v.visitor_type ?? "",
            hasPhoto = v.HasPhoto == 1,
            // 代录的记录我只看得到、不能处理（审批/签到/签离只能被访人本人）
            canHandle = v.Person_ID == Pid,
            submittedByMe = !string.IsNullOrEmpty(myOpenId) && v.OpenID == myOpenId
        }).ToList());
    }

    /// <summary>
    /// 被访人查询：按姓名/工号搜索在职员工（自助登记选被访人用）。
    /// 必须带关键字才返回：空关键字直接返回空列表，避免任何登录用户可枚举全员名单。
    /// </summary>
    [HttpGet("persons")]
    public IActionResult Persons(string keyword = "")
    {
        string kw = Trim(keyword);
        if (kw.Length == 0) return Ok(new List<object>());   // 不提供"默认名单"，必须先搜索

        var rows = _db.ST_People.AsNoTracking()
            .Where(p => !p.Is_Del && (p.Person_Name.Contains(kw) || p.Person_No.Contains(kw)))
            .OrderBy(p => p.Person_No)
            .Take(20)
            .Select(p => new { p.Person_ID, p.Person_No, p.Person_Name, p.Dept_ID })
            .ToList();

        var deptMap = _db.ST_Departments.AsNoTracking().ToList()
            .GroupBy(d => d.Dept_ID).ToDictionary(g => g.Key, g => g.First().Dept_Name ?? "");

        return Ok(rows.Select(p => new
        {
            personId = p.Person_ID,
            personNo = p.Person_No ?? "",
            personName = p.Person_Name ?? "",
            deptName = p.Dept_ID.HasValue && deptMap.ContainsKey(p.Dept_ID.Value) ? deptMap[p.Dept_ID.Value] : ""
        }).ToList());
    }

    /// <summary>访客照片：优先按申请单号取（列表里证件号已脱敏，不能用来查），返回 dataURL</summary>
    [HttpGet("photo")]
    public IActionResult Photo(string applyNo = "", string idCard = "")
    {
        string id = Trim(idCard);
        if (id.Length == 0 && !string.IsNullOrEmpty(applyNo))
        {
            var apply = _db.VisitorApplies.AsNoTracking().FirstOrDefault(x => x.Apply_No == applyNo);
            if (apply != null) id = apply.VisitorID_number ?? "";
        }
        if (id.Length == 0) return Ok(new { photo = "" });
        var u = _db.VisitorUsers.AsNoTracking().FirstOrDefault(x => x.VisitorID_number == id);
        if (u?.VisitorPhoto == null || u.VisitorPhoto.Length == 0)
            return Ok(new { photo = "" });
        return Ok(new { photo = "data:image/jpeg;base64," + Convert.ToBase64String(u.VisitorPhoto) });
    }

    // ================= 提交预约 =================

    /// <summary>提交访客预约：先 upsert VisitorUser 档案，再写 VisitorApply 申请单</summary>
    [HttpPost("submit")]
    public IActionResult Submit([FromBody] VisitorSubmitReq req)
    {
        if (req == null) return Ok(new { success = false, msg = "参数错误" });

        string name = Trim(req.visitorName);
        string idCard = Trim(req.idCard);
        string phone = Trim(req.visitorPhone);
        string company = Trim(req.company);
        string reason = Trim(req.reason);
        string typeText = Trim(req.visitorType);

        if (name.Length == 0) return Ok(new { success = false, msg = "请填写访客姓名" });
        if (idCard.Length < 6) return Ok(new { success = false, msg = "请填写有效的证件号码" });
        if (!Regex.IsMatch(phone, @"^1[3-9]\d{9}$"))
            return Ok(new { success = false, msg = "请填写访客手机号（用于核验与联系）" });
        if (reason.Length == 0) return Ok(new { success = false, msg = "请填写来访事由" });

        if (!DateTime.TryParse(req.visitDate, out var day))
            return Ok(new { success = false, msg = "请选择来访日期" });

        if (!ParseTimeRange(day, req.visitTime ?? "", out var start, out var end))
            return Ok(new { success = false, msg = "来访时段格式应为 09:00-11:00" });
        if (end < DateTime.Now)
            return Ok(new { success = false, msg = "来访时间已过去，请选择未来的时段" });

        // 被访人：自助登记时由前端选择（personId），未传则默认当前登录人
        int respondentId = (req.personId.HasValue && req.personId.Value > 0) ? req.personId.Value : Pid;
        var respondent = _db.ST_People.FirstOrDefault(x => x.Person_ID == respondentId && !x.Is_Del);
        if (respondent == null) return Ok(new { success = false, msg = "被访人不存在或已离职，请重新选择" });

        // 同一证件号 + 同一被访人 + 同一天，未终结的申请只允许一条
        int dup = _db.Database.SqlQueryRaw<int>(
            @"SELECT COUNT(1) AS Value FROM VisitorApply
              WHERE VisitorID_number = @p0 AND Person_ID = @p1
                AND VisitOrNot IN (0,1) AND CAST(Start_Date AS date) = @p2",
            new SqlParameter("@p0", idCard),
            new SqlParameter("@p1", respondentId),
            new SqlParameter("@p2", day.Date)).FirstOrDefault();
        if (dup > 0) return Ok(new { success = false, msg = "该访客当天已有预约，请勿重复提交" });

        if (name.Length > 50) name = name.Substring(0, 50);
        if (company.Length > 200) company = company.Substring(0, 200);
        if (reason.Length > 200) reason = reason.Substring(0, 200);
        if (typeText.Length == 0) typeText = "临时访客";
        if (typeText.Length > 20) typeText = typeText.Substring(0, 20);
        if (idCard.Length > 30) return Ok(new { success = false, msg = "证件号码过长" });

        UpsertVisitorUser(idCard, name, phone, company);

        // 访客照片（可选）：存 VisitorUser.VisitorPhoto，供前台核验时放大查看
        if (!string.IsNullOrEmpty(req.photo))
        {
            var photo = ParsePhoto(req.photo!, out var photoErr);
            if (photo == null) return Ok(new { success = false, msg = photoErr });
            _db.Database.ExecuteSqlRaw(
                "UPDATE VisitorUser SET VisitorPhoto = @p0, VisitorPhotoUrl = 'wx', update_time = GETDATE() WHERE VisitorID_number = @p1",
                new SqlParameter("@p0", photo),
                new SqlParameter("@p1", idCard));
        }

        string submitterOpenId = CurrentOpenId();
        string applyNo = "VIS" + DateTime.Now.ToString("yyyyMMddHHmmss")
                       + Random.Shared.Next(100, 1000);
        string visitNo = Random.Shared.Next(100000, 999999).ToString();

        _db.VisitorApplies.Add(new VisitorApply
        {
            Apply_No = applyNo,
            Person_ID = respondentId,
            Person_Mobile = respondent.mobile ?? "",
            Visitor_Number = visitNo,                 // 预约码，不是姓名
            VisitorID_number = idCard,
            Visitor_Company = company,
            Subject_Matter = reason,
            Start_Date = start,
            End_Date = end,
            visitors_number = 1 + Math.Max(0, req.companions),   // 含访客本人
            car_number = Trim(req.plate),
            Carry_items = Trim(req.carryItems),
            visitor_type = typeText,
            VisitOrNot = 0,
            OpenID = submitterOpenId,          // 提交人快照（被访人代录时也记下是谁录的）
            CreateTime = DateTime.Now,
            update_time = DateTime.Now
        });
        _db.SaveChanges();

        // isSelf：被访人就是当前提交人。为别人登记时记录归对方，提交人列表里看不到，需要提示
        bool isSelf = respondentId == Pid;
        return Ok(new { success = true, applyNo, visitNo, respondentName = respondent.Person_Name ?? "", isSelf });
    }

    /// <summary>可用临时卡（审批/发卡时可选；卡池为空时返回空列表）</summary>
    [HttpGet("cards")]
    public IActionResult Cards()
    {
        try
        {
            var cards = VisitorCardPool.FreeCards();
            return Ok(new
            {
                total = cards.Count,
                cards = cards.Select(c => new
                {
                    cardNo = c.Card_No,
                    personName = c.Person_Name ?? "",          // 卡绑定的访客人员（现场便于对号）
                    personNo = c.Person_No ?? "",
                    validDate = c.Card_ValidDate.HasValue ? c.Card_ValidDate.Value.ToString("yyyy-MM-dd HH:mm") : "",
                    remark = c.Person_Name ?? ""               // 兼容旧前端字段
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            // 结构异常时不要让审批页面报错，按“暂无可用卡”处理
            return Ok(new { total = 0, cards = new List<object>(), msg = ex.Message });
        }
    }

    // ================= 审批 =================

    /// <summary>审批通过：分配临时卡（有卡时）+ 生成二维码凭证</summary>
    [HttpPost("approve")]
    public IActionResult Approve([FromBody] VisitorApproveReq req)
    {
        var v = Find(req);
        if (v == null) return Ok(new { success = false, msg = "记录不存在" });
        if (v.VisitOrNot != 0) return Ok(new { success = false, msg = "该申请已审批" });

        string? cardNo = null;
        string cardTip;
        if (req!.noCard)
        {
            // 审批人明确选择“不分配卡”：只发二维码
            cardTip = "未分配临时卡，仅生成二维码凭证";
        }
        else
        {
            // 卡池为空不阻断审批，降级为只发二维码
            try { cardNo = VisitorCardPool.Allocate(v.Apply_No, ExpireOf(v), req.cardNo, out cardTip); }
            catch (Exception ex) { cardTip = "临时卡分配失败：" + ex.Message; }
        }

        v.VisitOrNot = 1;
        v.Approved_Reason = "同意";
        v.Approved_User = CurrentPersonNo();
        v.Approved_Time = DateTime.Now;
        v.Empower_Card_No = string.IsNullOrEmpty(cardNo) ? null : cardNo;
        v.update_time = DateTime.Now;
        _db.SaveChanges();

        var token = BuildToken(v.Apply_No, ExpireOf(v));
        var visitor = VisitorUserOf(v.VisitorID_number);
        string who = visitor != null && !string.IsNullOrEmpty(visitor.Visitor_Name) ? visitor.Visitor_Name : "访客";

        MobileMessageService.Push(Pid, "visitor", "访客预约已通过",
            string.Format("{0} 的来访申请已通过{1}，请在「访客记录」出示凭证并转交访客。",
                who, string.IsNullOrEmpty(cardNo) ? "（仅二维码凭证）" : "，临时卡 " + cardNo));

        return Ok(new { success = true, passCode = cardNo ?? v.Visitor_Number, cardNo = cardNo ?? "", visitToken = token, cardTip });
    }

    /// <summary>驳回：VisitOrNot=2 + 原因</summary>
    [HttpPost("reject")]
    public IActionResult Reject([FromBody] VisitorRejectReq req)
    {
        if (req == null) return Ok(new { success = false, msg = "参数错误" });
        var v = Find(new VisitorActionReq { id = req.id });
        if (v == null) return Ok(new { success = false, msg = "记录不存在" });
        if (v.VisitOrNot != 0) return Ok(new { success = false, msg = "该申请已审批" });

        string reason = Trim(req.reason);
        if (reason.Length == 0) return Ok(new { success = false, msg = "请填写驳回原因" });
        if (reason.Length > 300) reason = reason.Substring(0, 300);

        v.VisitOrNot = 2;
        v.Approved_Reason = reason;
        v.Approved_User = CurrentPersonNo();
        v.Approved_Time = DateTime.Now;
        v.update_time = DateTime.Now;
        _db.SaveChanges();

        VisitorCardPool.Release(v.Apply_No);
        MobileMessageService.Push(Pid, "visitor", "访客预约被驳回",
            string.Format("访客预约（{0}）被驳回：{1}", v.Apply_No, reason));
        return Ok(new { success = true });
    }

    // ================= 到访签到 / 签离 =================

    /// <summary>到访签到：写 check_intime（审批通过 ≠ 已到访）</summary>
    [HttpPost("checkin")]
    public IActionResult CheckIn([FromBody] VisitorApproveReq req)
    {
        var v = Find(new VisitorActionReq { id = req?.id });
        if (v == null) return Ok(new { success = false, msg = "记录不存在" });
        if (v.VisitOrNot != 1) return Ok(new { success = false, msg = "只有已通过的预约才能签到" });
        if (v.check_intime.HasValue) return Ok(new { success = false, msg = "该访客已签到" });
        if (v.End_Date.HasValue && v.End_Date.Value < DateTime.Now)
            return Ok(new { success = false, msg = "预约时段已过，无法签到" });

        v.check_intime = DateTime.Now;
        v.update_time = DateTime.Now;
        _db.SaveChanges();

        // 签到补发卡：审批时卡池没卡 / 现场才发卡的情况，此处可选卡也可不选（不选=自动或只发二维码）
        string cardNo = "", cardTip = "";
        if (string.IsNullOrEmpty(v.Empower_Card_No) && !(req?.noCard ?? false))
        {
            try { cardNo = VisitorCardPool.Allocate(v.Apply_No, ExpireOf(v), req?.cardNo, out cardTip) ?? ""; }
            catch (Exception ex) { cardTip = "临时卡分配失败：" + ex.Message; }
            if (!string.IsNullOrEmpty(cardNo))
            {
                v.Empower_Card_No = cardNo;
                v.update_time = DateTime.Now;
                _db.SaveChanges();
            }
        }
        else
        {
            cardNo = v.Empower_Card_No ?? "";
            if (req?.noCard ?? false) cardTip = "未分配临时卡，仅二维码凭证";
        }

        return Ok(new
        {
            success = true,
            checkinTime = v.check_intime.Value.ToString("yyyy-MM-dd HH:mm"),
            cardNo,
            cardTip
        });
    }

    /// <summary>签离：写 check_outtime，并释放临时卡</summary>
    [HttpPost("signout")]
    public IActionResult SignOut([FromBody] VisitorActionReq req)
    {
        var v = Find(req);
        if (v == null) return Ok(new { success = false, msg = "记录不存在" });
        if (v.check_outtime.HasValue) return Ok(new { success = false, msg = "该访客已签离" });
        if (!v.check_intime.HasValue) return Ok(new { success = false, msg = "访客尚未签到，不能签离" });

        v.check_outtime = DateTime.Now;
        v.update_time = DateTime.Now;
        _db.SaveChanges();

        VisitorCardPool.Release(v.Apply_No);   // 卡立即作废
        return Ok(new { success = true, signOutTime = v.check_outtime.Value.ToString("yyyy-MM-dd HH:mm") });
    }

    /// <summary>作废：已通过但访客没来（未签到）时，回收临时卡并关闭该预约</summary>
    [HttpPost("revoke")]
    public IActionResult Revoke([FromBody] VisitorActionReq req)
    {
        var v = Find(req);
        if (v == null) return Ok(new { success = false, msg = "记录不存在" });
        if (v.VisitOrNot != 1) return Ok(new { success = false, msg = "只有已通过的预约可以作废" });
        if (v.check_intime.HasValue) return Ok(new { success = false, msg = "访客已到访，请走签离" });
        if (!v.check_outtime.HasValue && v.End_Date.HasValue && v.End_Date.Value > DateTime.Now)
            return Ok(new { success = false, msg = "预约时段未结束，不能作废" });

        // 不改 VisitOrNot：保留「曾经审批通过」的事实，状态由时间推导为 expired
        v.Approved_Reason = Trim(v.Approved_Reason) + "（未到访作废）";
        if (v.Approved_Reason.Length > 300) v.Approved_Reason = v.Approved_Reason.Substring(0, 300);
        v.update_time = DateTime.Now;
        _db.SaveChanges();

        VisitorCardPool.Release(v.Apply_No);
        return Ok(new { success = true });
    }

    // ================= 凭证核验（前台/门点扫码，匿名） =================

    /// <summary>校验二维码凭证：签名 + 有效期 + 单据状态；只返回核验所需的脱敏信息</summary>
    [AllowAnonymous]
    [HttpGet("verify")]
    public IActionResult Verify(string token)
    {
        if (!VerifyToken(token, out var applyNo, out var expire))
            return Ok(new { valid = false, reason = "凭证无效或已被篡改" });
        if (expire < DateTime.Now)
            return Ok(new { valid = false, reason = "凭证已过期" });

        var v = _db.VisitorApplies.FirstOrDefault(x => x.Apply_No == applyNo);
        if (v == null) return Ok(new { valid = false, reason = "预约单不存在" });
        if (v.VisitOrNot != 1) return Ok(new { valid = false, reason = "预约未通过审批" });
        if (v.check_outtime.HasValue) return Ok(new { valid = false, reason = "访客已签离" });
        if (v.End_Date.HasValue && v.End_Date.Value < DateTime.Now)
        {
            try { VisitorCardPool.Release(v.Apply_No); } catch { /* 忽略 */ }
            return Ok(new { valid = false, reason = "已超过预约时段" });
        }

        var visitor = VisitorUserOf(v.VisitorID_number);
        return Ok(new
        {
            valid = true,
            applyNo = v.Apply_No,
            visitorName = visitor?.Visitor_Name ?? "",
            visitorPhone = MaskPhone(visitor?.PhoneNumber ?? ""),
            company = v.Visitor_Company ?? "",
            start = v.Start_Date.HasValue ? v.Start_Date.Value.ToString("yyyy-MM-dd HH:mm") : "",
            end = v.End_Date.HasValue ? v.End_Date.Value.ToString("yyyy-MM-dd HH:mm") : "",
            cardNo = v.Empower_Card_No ?? "",
            checkinTime = v.check_intime.HasValue ? v.check_intime.Value.ToString("yyyy-MM-dd HH:mm") : "",
            state = v.check_intime.HasValue ? "已签到" : "待签到"
        });
    }

    // ================= 通行凭证（后台只给内容，图片由前端生成） =================

    /// <summary>
    /// 通行凭证内容：后端只签发二维码「内容」，图片由前端绘制。
    /// content 与 /verify 使用同一套签名 token（VIS1.ApplyNo.Expire.Hmac）。
    /// 状态不满足（未通过 / 已签离 / 已过时段）时不下发内容，返回可读原因，前端按 code 提示。
    /// </summary>
    [HttpGet("qrcode")]
    public IActionResult QrCode(string id = "")
    {
        var v = FindById(id);
        if (v == null) return Ok(new { success = false, code = "VISITOR_NOT_FOUND", msg = "记录不存在" });

        DateTime now = DateTime.Now;
        if (v.VisitOrNot == 2) return Ok(new { success = false, code = "VISITOR_REJECTED", msg = "预约已被驳回，凭证不生成" });
        if (v.VisitOrNot != 1) return Ok(new { success = false, code = "VISITOR_PENDING", msg = "预约还在审批中，审批通过后才会生成二维码凭证" });
        if (v.check_outtime.HasValue) return Ok(new { success = false, code = "VISITOR_SIGNED_OUT", msg = "访客已签离，凭证已失效" });

        DateTime expire = ExpireOf(v);
        if (expire < now) return Ok(new { success = false, code = "VISITOR_EXPIRED", msg = "已超过预约时段，凭证已失效；如需来访请重新预约" });

        string token = BuildToken(v.Apply_No, expire);
        if (!string.IsNullOrEmpty(v.Empower_Card_No))
        {
            // 与 Net 版一致：一卡通设备认「employee;工号;过期unix」这种通行码
            var p = _db.ST_People.FirstOrDefault(x => x.Card_No == v.Empower_Card_No);
            if (p != null)
                token = "employee;" + (p.Person_No ?? "") + ";" + ToUnix(expire);
        }
        return Ok(new
        {
            success = true,
            applyNo = v.Apply_No,
            content = token,                                // 二维码内容：扫出来就是这串
            token = token,                                  // 兼容旧字段名
            expireAt = expire.ToString("yyyy-MM-dd HH:mm"),
            cardNo = v.Empower_Card_No ?? ""
        });
    }

    // ================= 内部 =================

    // 审批接口用的是 VisitorApproveReq（多了 cardNo），这里给两个重载统一走同一个实现
    private VisitorApply? Find(VisitorActionReq? req)
    {
        return req == null ? null : FindById(req.id);
    }

    private VisitorApply? Find(VisitorApproveReq? req)
    {
        return req == null ? null : FindById(req.id);
    }

    /// <summary>只有被访人本人能审批 / 签到 / 签离</summary>
    private VisitorApply? FindById(string? id)
    {
        if (!int.TryParse(id, out int key)) return null;
        return _db.VisitorApplies.FirstOrDefault(x => x.ID_KEY == key && x.Person_ID == Pid);
    }

    private VisitorUser? VisitorUserOf(string? idCard)
    {
        if (string.IsNullOrEmpty(idCard)) return null;
        return _db.VisitorUsers.AsNoTracking().FirstOrDefault(x => x.VisitorID_number == idCard);
    }

    private void UpsertVisitorUser(string idCard, string name, string phone, string company)
    {
        _db.Database.ExecuteSqlRaw(
            @"IF EXISTS (SELECT 1 FROM VisitorUser WHERE VisitorID_number = @p0)
                  UPDATE VisitorUser
                  SET Visitor_Name = @p1, PhoneNumber = @p2, Visitor_Company = @p3, update_time = GETDATE()
                  WHERE VisitorID_number = @p0
              ELSE
                  INSERT INTO VisitorUser (VisitorID_number, ID_Type, Visitor_Name, PhoneNumber, Visitor_Company, CreateTime, update_time)
                  VALUES (@p0, N'身份证', @p1, @p2, @p3, GETDATE(), GETDATE())",
            new SqlParameter("@p0", idCard),
            new SqlParameter("@p1", name),
            new SqlParameter("@p2", phone),
            new SqlParameter("@p3", company));
    }

    /// <summary>
    /// 当前登录人的微信身份：H5 绑定 OpenID、小程序绑定 UnionID，取到哪个用哪个。
    /// 只作为「提交人快照」写 VisitorApply.OpenID，用于“我提交的访客记录”查询；
    /// 不写 VisitorUser.OpenID —— 那是访客本人的档案，写代录人的 openid 会串档。
    /// </summary>
    private string CurrentOpenId()
    {
        var p = _db.ST_People.AsNoTracking().FirstOrDefault(x => x.Person_ID == Pid);
        if (p == null) return "";
        if (!string.IsNullOrEmpty(p.OpenID)) return p.OpenID.Trim();
        if (!string.IsNullOrEmpty(p.UnionID)) return p.UnionID.Trim();
        return "";
    }

    private string CurrentPersonNo()
    {
        var p = _db.ST_People.AsNoTracking().FirstOrDefault(x => x.Person_ID == Pid);
        return p != null ? (p.Person_No ?? "WECHAT") : "WECHAT";
    }

    /// <summary>被访人所在部门（用已批量加载的字典，避免逐条查询）</summary>
    private static string DeptNameOf(int? personId, Dictionary<int, string> deptNames, Dictionary<int, int?> personDept)
    {
        if (!personId.HasValue) return "";
        if (!personDept.TryGetValue(personId.Value, out var deptId) || !deptId.HasValue) return "";
        return deptNames.TryGetValue(deptId.Value, out var n) ? n : "";
    }

    /// <summary>状态：签离 &gt; 已签到 &gt; 驳回 &gt; 通过（或过期）&gt; 待审批</summary>
    private static string StatusOf(VisitorRow v, DateTime now)
    {
        if (v.check_outtime.HasValue) return "completed";
        if (v.check_intime.HasValue) return "checkedin";
        if (v.VisitOrNot == 2) return "rejected";
        if (v.VisitOrNot == 1)
            return (v.End_Date.HasValue && v.End_Date.Value < now) ? "expired" : "approved";
        return "pending";
    }

    /// <summary>解析 visitDate + "09:00-11:00" 为起止时刻；未填时间则为全天</summary>
    private static bool ParseTimeRange(DateTime day, string visitTime, out DateTime start, out DateTime end)
    {
        start = day.Date;
        end = day.Date.AddDays(1).AddSeconds(-1);
        if (string.IsNullOrEmpty(visitTime)) return true;

        string[] parts = visitTime.Replace('～', '-').Replace('~', '-').Split('-');
        if (parts.Length != 2) return false;
        if (!TimeSpan.TryParse(parts[0].Trim(), out var ts)) return false;
        if (!TimeSpan.TryParse(parts[1].Trim(), out var te)) return false;
        start = day.Date.Add(ts);
        end = day.Date.Add(te);
        return end > start;
    }

    private static string TimeRange(DateTime? start, DateTime? end)
    {
        if (!start.HasValue) return "";
        string s = start.Value.ToString("HH:mm");
        if (!end.HasValue) return s;
        return s + "-" + end.Value.ToString("HH:mm");
    }

    // ---- 二维码凭证签名 ----

    private static string QrSecret
    {
        get
        {
            string s = AppConfig.VisitorQrSecret;
            if (string.IsNullOrEmpty(s)) s = AppConfig.JwtSecret;
            return string.IsNullOrEmpty(s) ? "visitor-qr-secret" : s;
        }
    }

    private static DateTime ExpireOf(VisitorApply v)
    {
        return v.End_Date ?? DateTime.Now.AddDays(1);
    }

    private static string BuildTokenIfValid(VisitorRow v, DateTime now)
    {
        if (v.VisitOrNot != 1) return "";
        if (v.check_outtime.HasValue) return "";
        var exp = v.End_Date ?? now.AddDays(1);
        if (exp < now) return "";
        return BuildToken(v.Apply_No ?? "", exp);
    }

    private static string BuildToken(string applyNo, DateTime expire)
    {
        long exp = ToUnix(expire);
        return "VIS1." + applyNo + "." + exp + "." + Hmac(applyNo + "|" + exp);
    }

    private static bool VerifyToken(string? token, out string applyNo, out DateTime expire)
    {
        applyNo = "";
        expire = DateTime.MinValue;
        if (string.IsNullOrEmpty(token)) return false;

        string[] parts = token.Trim().Split('.');
        if (parts.Length != 4 || parts[0] != "VIS1") return false;

        if (!long.TryParse(parts[2], out var exp)) return false;
        if (!string.Equals(parts[3], Hmac(parts[1] + "|" + exp), StringComparison.OrdinalIgnoreCase)) return false;

        applyNo = parts[1];
        expire = FromUnix(exp);
        return true;
    }

    private static string Hmac(string raw)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(QrSecret));
        byte[] hash = h.ComputeHash(Encoding.UTF8.GetBytes(raw));
        var sb = new StringBuilder(32);
        for (int i = 0; i < 16; i++) sb.Append(hash[i].ToString("x2"));
        return sb.ToString();
    }

    private static long ToUnix(DateTime t)
        => (long)(t.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

    private static DateTime FromUnix(long seconds)
        => new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds).ToLocalTime();

    // ---- 脱敏（仅展示层，库里存明文） ----

    private static string MaskPhone(string? phone)
    {
        if (string.IsNullOrEmpty(phone) || phone.Length < 7) return phone ?? "";
        return phone.Substring(0, 3) + "****" + phone.Substring(phone.Length - 4);
    }

    private static string MaskIdCard(string? idCard)
    {
        if (string.IsNullOrEmpty(idCard) || idCard.Length < 8) return idCard ?? "";
        return idCard.Substring(0, 4) + "**********" + idCard.Substring(idCard.Length - 4);
    }

    /// <summary>解析 dataURL 图片（限制 2MB），失败返回 null 并给出原因</summary>
    private static byte[]? ParsePhoto(string dataUrl, out string err)
    {
        err = "";
        string raw = (dataUrl ?? "").Trim();
        int comma = raw.IndexOf(',');
        if (comma > 0) raw = raw.Substring(comma + 1);
        if (raw.Length == 0) { err = "照片数据为空"; return null; }
        try
        {
            byte[] bytes = Convert.FromBase64String(raw);
            if (bytes.Length == 0) { err = "照片数据为空"; return null; }
            if (bytes.Length > 2 * 1024 * 1024) { err = "照片不能超过 2MB"; return null; }
            return bytes;
        }
        catch
        {
            err = "照片格式不正确";
            return null;
        }
    }

    private static string Trim(string? s) => (s ?? "").Trim();

    public class VisitorRow
    {
        public int ID_KEY { get; set; }
        public string? Apply_No { get; set; }
        public int? Person_ID { get; set; }
        public string? Person_Mobile { get; set; }
        public string? Visitor_Number { get; set; }
        public DateTime? Start_Date { get; set; }
        public DateTime? End_Date { get; set; }
        public int? VisitOrNot { get; set; }
        public string? Subject_Matter { get; set; }
        public string? Carry_items { get; set; }
        public DateTime? CreateTime { get; set; }
        public string? VisitorID_number { get; set; }
        public string? car_number { get; set; }
        public string? Visitor_Company { get; set; }
        public string? visitor_type { get; set; }
        public string? Empower_Card_No { get; set; }
        public string? Approved_User { get; set; }
        public DateTime? Approved_Time { get; set; }
        public string? Approved_Reason { get; set; }
        public DateTime? check_intime { get; set; }
        public DateTime? check_outtime { get; set; }
        public DateTime update_time { get; set; }
        public int visitors_number { get; set; }
        public string? OpenID { get; set; }
        public string? Visitor_Name { get; set; }
        public string? PhoneNumber { get; set; }
        public int HasPhoto { get; set; }
    }
}

public class VisitorSubmitReq
{
    public string? visitorName { get; set; }
    public string? visitorPhone { get; set; }
    public string? idCard { get; set; }
    public string? company { get; set; }
    public string? reason { get; set; }
    public string? visitDate { get; set; }     // yyyy-MM-dd
    public string? visitTime { get; set; }     // 09:00-11:00
    public int companions { get; set; }
    public string? plate { get; set; }
    public string? carryItems { get; set; }
    public string? visitorType { get; set; }   // 中文，如「供应商」
    public int? personId { get; set; }         // 被访人 Person_ID（自助登记选择）
    public string? photo { get; set; }         // 访客照片 dataURL（可选，≤2MB）
}

public class VisitorActionReq { public string? id { get; set; } }

public class VisitorApproveReq
{
    public string? id { get; set; }
    /// <summary>指定临时卡号；为空则自动分配第一张空闲卡</summary>
    public string? cardNo { get; set; }
    /// <summary>true = 明确不分配临时卡（只发二维码），忽略 cardNo</summary>
    public bool noCard { get; set; }
}

public class VisitorRejectReq { public string? id { get; set; } public string? reason { get; set; } }

public class PersonNameRow
{
    public int Person_ID { get; set; }
    public string? Person_Name { get; set; }
    public int? Dept_ID { get; set; }
}

public class DeptRow
{
    public int Dept_ID { get; set; }
    public string? Dept_Name { get; set; }
}
