using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PsnMgrService.Models;
using PsnMgrService.Services;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

namespace PsnMgrService.Controllers.MicroApi;

/// <summary>
/// 移动端查询（第一批，对应 Net 版 MobileQueryController 的 access / attendance / leave 部分）。
///   access/*       通行记录与门禁权限（附加8）
///   attendance/daily 我的考勤（附加10/11）
///   leave/*         请假（附加9）
/// JSON 字段名与 Net 版一致（默认 camelCase 序列化）。
/// </summary>
[ApiController]
[Authorize]
[Route("api/m")]
public class MobileAccessController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<MobileAccessController> _logger;

    public MobileAccessController(AppDbContext db, ILogger<MobileAccessController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private int Pid => int.TryParse(User.FindFirst("PersonId")?.Value, out var v) ? v : 0;

    // ================= 门禁通行记录 / 门禁权限 =================

    /// <summary>事件字典：Event_Code → 事件信息（Event_Code 在不同 Event_Type 下可能重复，取第一条）</summary>
    private Dictionary<int, MJ_EventInfo> EventDict()
    {
        return _db.MJ_EventInfos.AsNoTracking().ToList()
            .GroupBy(x => (int)x.Event_Code)
            .ToDictionary(g => g.Key, g => g.First());
    }

    /// <summary>门字典：(Ctrl_No|Door_No) → 门名称，用于补全记录里缺失的 Door_Name</summary>
    private Dictionary<string, MJ_Door> DoorDict()
    {
        return _db.MJ_Doors.AsNoTracking().ToList()
            .GroupBy(x => DoorKey(x.Ctrl_No, x.Door_No))
            .ToDictionary(g => g.Key, g => g.First());
    }

    private static string DoorKey(string? ctrlNo, int? doorNo)
        => (ctrlNo ?? "").Trim() + "|" + (doorNo.HasValue ? doorNo.Value.ToString() : "");

    /// <summary>门名称：优先用记录里的 Door_Name（历史快照），为空再按 Ctrl_No+Door_No 回查 MJ_Door</summary>
    private static string ResolveDoorName(string? doorName, string? ctrlNo, int? doorNo, Dictionary<string, MJ_Door> doors)
    {
        if (!string.IsNullOrEmpty(doorName)) return doorName!;
        if (doors != null && doors.TryGetValue(DoorKey(ctrlNo, doorNo), out var d) && d != null && !string.IsNullOrEmpty(d.Door_Name))
            return d.Door_Name;
        return (ctrlNo ?? "") + (doorNo.HasValue ? "-" + doorNo.Value : "");
    }

    /// <summary>事件名称（通行方式 / 异常原因），查不到返回空串</summary>
    private static string EventName(int? eventCode, Dictionary<int, MJ_EventInfo> events)
    {
        if (!eventCode.HasValue || events == null) return "";
        return events.TryGetValue(eventCode.Value, out var e) && e != null ? (e.Event_Name ?? "") : "";
    }

    /// <summary>正常通行记录（MJ_NormalRecord，通行方式取自 MJ_EventInfo）</summary>
    [HttpGet("access/normal")]
    public async Task<IActionResult> AccessNormal(string bgn, string end, int page = 1, int size = 20)
    {
        if (page < 1) page = 1;
        if (size < 1 || size > 200) size = 20;
        // 起止都按「当天」闭区间处理：bgn 00:00 ~ end 次日 00:00（不含），避免多算一天
        DateTime dtBgn = ParseDate(bgn, DateTime.Now.AddDays(-30)).Date;
        DateTime dtEnd = ParseDate(end, DateTime.Now).Date.AddDays(1);

        var events = EventDict();
        var doors = DoorDict();
        // EF 的 Skip/Take 会生成 OFFSET/FETCH（需 compat 110+），2008 上必须用 ROW_NUMBER
        var rows = (await Db.QueryAsync<NormalRecRow>(@"
SELECT * FROM (
    SELECT ID_KEY, BrushCard_Time, Ctrl_No, Door_No, Door_Name, Card_No, Event_Code, Is_InDoor,
           ROW_NUMBER() OVER (ORDER BY BrushCard_Time DESC) AS RowNum
    FROM MJ_NormalRecord
    WHERE Person_ID = @pid AND BrushCard_Time >= @bgn AND BrushCard_Time < @end
) T WHERE T.RowNum BETWEEN @start AND @endRow ORDER BY T.BrushCard_Time DESC",
            new { pid = Pid, bgn = dtBgn, end = dtEnd, start = (page - 1) * size + 1, endRow = page * size })).ToList();

        return Ok(rows.Select(r => new
        {
            id = r.ID_KEY,
            time = r.BrushCard_Time,
            ctrlNo = r.Ctrl_No,
            doorNo = r.Door_No,
            door = ResolveDoorName(r.Door_Name, r.Ctrl_No, r.Door_No, doors),
            cardNo = r.Card_No,
            eventCode = r.Event_Code,
            passType = EventName(r.Event_Code, events),
            inDoor = r.Is_InDoor,
            direction = r.Is_InDoor ? "进" : "出",
            result = "success"
        }).ToList());
    }

    /// <summary>
    /// 异常通行记录（MJ_AbnormRecord）。该表没有 Person_ID，只能按卡号匹配：
    /// 卡号取 ST_Person.Card_No；异常原因取 MJ_EventInfo.Event_Name。
    /// </summary>
    [HttpGet("access/abnorm")]
    public async Task<IActionResult> AccessAbnorm(string bgn, string end, int page = 1, int size = 20)
    {
        if (page < 1) page = 1;
        if (size < 1 || size > 200) size = 20;
        DateTime dtBgn = ParseDate(bgn, DateTime.Now.AddDays(-30)).Date;
        DateTime dtEnd = ParseDate(end, DateTime.Now).Date.AddDays(1);

        string? cardNo = _db.ST_People.AsNoTracking().Where(p => p.Person_ID == Pid).Select(p => p.Card_No).FirstOrDefault();
        if (string.IsNullOrEmpty(cardNo)) return Ok(new List<object>());

        var events = EventDict();
        var doors = DoorDict();
        var rows = (await Db.QueryAsync<AbnormRecRow>(@"
SELECT * FROM (
    SELECT ID_KEY, BrushCard_Time, Ctrl_No, Door_No, Door_Name, Card_No, Event_Code, Is_InDoor,
           ROW_NUMBER() OVER (ORDER BY BrushCard_Time DESC) AS RowNum
    FROM MJ_AbnormRecord
    WHERE Card_No = @cardNo AND BrushCard_Time >= @bgn AND BrushCard_Time < @end
) T WHERE T.RowNum BETWEEN @start AND @endRow ORDER BY T.BrushCard_Time DESC",
            new { cardNo, bgn = dtBgn, end = dtEnd, start = (page - 1) * size + 1, endRow = page * size })).ToList();

        return Ok(rows.Select(r =>
        {
            string name = EventName(r.Event_Code, events);
            return new
            {
                id = r.ID_KEY,
                time = r.BrushCard_Time,
                ctrlNo = r.Ctrl_No,
                doorNo = r.Door_No,
                door = ResolveDoorName(r.Door_Name, r.Ctrl_No, r.Door_No, doors),
                cardNo = r.Card_No,
                eventCode = r.Event_Code,
                reason = string.IsNullOrEmpty(name) ? "未记录的异常" : name,
                inDoor = r.Is_InDoor,
                direction = r.Is_InDoor ? "进" : "出",
                result = "failed"
            };
        }).ToList());
    }

    /// <summary>门列表（MJ_Door + MJ_Controller），供通行记录页做门筛选，避免前端硬编码门名</summary>
    [HttpGet("access/doors")]
    public IActionResult AccessDoors()
    {
        var ctrls = _db.MJ_Controllers.AsNoTracking().ToList()
            .GroupBy(x => x.Ctrl_No).ToDictionary(g => g.Key, g => g.First().Ctrl_Name);
        var rows = _db.MJ_Doors.AsNoTracking().ToList()
            .OrderBy(x => x.Ctrl_No).ThenBy(x => x.Door_No)
            .Select(x =>
            {
                ctrls.TryGetValue(x.Ctrl_No, out var ctrlName);
                return new
                {
                    ctrlNo = x.Ctrl_No,
                    doorNo = x.Door_No,
                    doorName = x.Door_Name,
                    ctrlName = ctrlName ?? ""
                };
            }).ToList();
        return Ok(rows);
    }

    /// <summary>最近一次通行（正常记录最新一条），通行码页「最近一次通行」用</summary>
    [HttpGet("access/latest")]
    public IActionResult AccessLatest()
    {
        var events = EventDict();
        var doors = DoorDict();
        var r = _db.MJ_NormalRecords.AsNoTracking()
            .Where(x => x.Person_ID == Pid)
            .OrderByDescending(x => x.BrushCard_Time)
            .FirstOrDefault();
        if (r == null) return Ok(new { hasData = false });

        return Ok(new
        {
            hasData = true,
            id = r.ID_KEY,
            time = r.BrushCard_Time,
            door = ResolveDoorName(r.Door_Name, r.Ctrl_No, r.Door_No, doors),
            passType = EventName(r.Event_Code, events),
            inDoor = r.Is_InDoor,
            direction = r.Is_InDoor ? "进" : "出"
        });
    }

    /// <summary>
    /// 当前人员的门禁权限：MJ_PsnDoorRight JOIN MJ_Door / MJ_Controller，LEFT JOIN 时段。
    /// TimeSect_ID = -1 的行是周期天数定义，不参与「当前是否在时段内」判断；无时段或无有效时间窗 → 全天可通行。
    /// </summary>
    [HttpGet("access/rights")]
    public IActionResult AccessRights()
    {
        var rights = _db.MJ_PsnDoorRights.AsNoTracking().Where(x => x.Person_ID == Pid).ToList();
        if (rights.Count == 0) return Ok(new List<object>());

        var doors = DoorDict();
        var ctrls = _db.MJ_Controllers.AsNoTracking().ToList()
            .GroupBy(x => x.Ctrl_No).ToDictionary(g => g.Key, g => g.First().Ctrl_Name);
        var timeNames = _db.MJ_PsnInOutTimes.AsNoTracking().ToList()
            .GroupBy(x => x.PsnTime_No).ToDictionary(g => g.Key, g => g.First().PsnTime_Name);
        var allSubs = _db.MJ_PsnInOutTimeSubs.AsNoTracking().ToList();

        TimeSpan now = DateTime.Now.TimeOfDay;
        var list = new List<object>();
        foreach (var r in rights.OrderBy(x => x.Ctrl_No).ThenBy(x => x.Door_No))
        {
            doors.TryGetValue(DoorKey(r.Ctrl_No, r.Door_No), out var door);
            ctrls.TryGetValue(r.Ctrl_No, out var ctrlName);

            string timeName = "";
            if (r.PsnTime_No.HasValue) timeNames.TryGetValue(r.PsnTime_No.Value, out timeName);

            // 只取有效时间窗（TimeSect_ID >= 1），-1 是周期天数定义
            var wins = r.PsnTime_No.HasValue
                ? allSubs.Where(s => s.PsnTime_No == r.PsnTime_No.Value && s.TimeSect_ID >= 1)
                         .OrderBy(s => s.Start_Time).ToList()
                : new List<MJ_PsnInOutTimeSub>();
            bool allDay = wins.Count == 0;
            bool inTime = allDay || wins.Any(w => InTimeWindow(now, w.Start_Time, w.End_Time));

            list.Add(new
            {
                ctrlNo = r.Ctrl_No,
                doorNo = r.Door_No,
                doorName = door != null ? door.Door_Name : ResolveDoorName(null, r.Ctrl_No, r.Door_No, doors),
                ctrlName = ctrlName ?? "",
                psnTimeNo = r.PsnTime_No ?? 0,
                psnTimeName = timeName ?? "",
                allDay,
                timeRanges = wins.Select(w => (w.Start_Time ?? "") + "-" + (w.End_Time ?? "")).ToList(),
                inTimeNow = inTime,
                firstCardOpen = r.FirstCardOpen
            });
        }
        return Ok(list);
    }

    /// <summary>当前时刻是否落在时段窗口内；起止相同视为全天，Start &gt; End 视为跨夜时段</summary>
    private static bool InTimeWindow(TimeSpan now, string? start, string? end)
    {
        if (!TryHhMm(start, out var s) || !TryHhMm(end, out var e)) return false;
        if (s == e) return true;
        return s < e ? (now >= s && now <= e) : (now >= s || now <= e);
    }

    /// <summary>解析 'HH:mm'（兼容 'HH:mm:ss'），失败返回 false</summary>
    private static bool TryHhMm(string? s, out TimeSpan v)
    {
        v = TimeSpan.Zero;
        if (string.IsNullOrEmpty(s)) return false;
        s = s.Trim();
        if (s.Length > 5) s = s.Substring(0, 5);
        if (TimeSpan.TryParse(s, out var tmp)) { v = tmp; return true; }
        return false;
    }

    // ================= 考勤 =================

    /// <summary>补算节流：同一人 5 分钟内只触发一次 KQ_pd_CalcDaily</summary>
    private static readonly ConcurrentDictionary<int, CalcGuard> CalcGuards = new();
    private const int CalcThrottleMinutes = 5;
    private const int AttendanceMaxRangeDays = 62;
    private const int CalcCommandTimeout = 120;
    /// <summary>缺失日补算后仍无记录（休息日、未排班、未入职）时，该日期多久内不再重复触发计算</summary>
    private const int AttemptedRetryMinutes = 30;

    private sealed class CalcGuard
    {
        public readonly object Sync = new();
        public DateTime LastCalc = DateTime.MinValue;
        public readonly Dictionary<string, DateTime> AttemptedDays = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// H5「我的考勤」取每日考勤。
    /// KQ_Account 中 Account_Flag=0 的记录给出分界 Date_Bgn：晚于该日取 KQ_Daily，否则取 KQ_DailyHistory，跨分界分两段查询合并。
    /// 需要从 KQ_Daily 取数时逐日检查缺失并补算（只算当前登录人、只算到今天）。
    /// </summary>
    [HttpGet("attendance/daily")]
    public IActionResult Attendance(string bgn, string end)
    {
        var sw = Stopwatch.StartNew();
        if (!TryParseYmd(bgn, out var bgnDate) || !TryParseYmd(end, out var endDate) || bgnDate > endDate)
            return Fail("INVALID_RANGE", "日期范围错误，格式 yyyy-MM-dd");
        if ((endDate - bgnDate).TotalDays + 1 > AttendanceMaxRangeDays)
            return Fail("RANGE_TOO_LARGE", "查询区间不能超过 " + AttendanceMaxRangeDays + " 天");

        List<AttendanceRow> rows;
        bool calculated;
        string source;
        Dictionary<string, ClassInfo> classes;
        try
        {
            // 查询与补算分离：EnsureCalculated 只负责把缺的算出来，GetAttendance 只读
            calculated = EnsureCalculated(bgnDate, endDate);
            rows = GetAttendance(bgnDate, endDate, out source, out classes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "考勤查询失败 PersonId={Pid} {Bgn}~{End}", Pid, Ymd(bgnDate), Ymd(endDate));
            return Fail("ATTENDANCE_QUERY_FAILED", "考勤数据读取失败，请稍后重试");
        }

        sw.Stop();
        _logger.LogInformation("attendance/daily PersonId={Pid} range={Bgn}~{End} source={Source} calculated={Calc} rows={Rows} elapsed={Ms}ms",
            Pid, Ymd(bgnDate), Ymd(endDate), source, calculated, rows.Count, sw.ElapsedMilliseconds);

        return Ok(new
        {
            success = true,
            source,                                        // daily | history | mixed | none
            calculated,                                    // 本次是否执行了 KQ_pd_CalcDaily
            asOf = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            rows,
            classes,                                       // 班次定义：Class_No -> { className, segments[] }
            summary = BuildSummary(rows)
        });
    }

    /// <summary>按账套分界取数：考勤日期 &lt;= Date_Bgn 取 KQ_DailyHistory，晚于它的取 KQ_Daily，合并后按日期排序。</summary>
    private List<AttendanceRow> GetAttendance(DateTime bgnDate, DateTime endDate,
        out string source, out Dictionary<string, ClassInfo> classes)
    {
        var classInfos = ClassInfoDict();
        var rows = new List<AttendanceRow>();
        bool hasHistory = false, hasDaily = false;
        DateTime? split = AccountSplitDate();

        // 历史段：考勤日期 <= Date_Bgn（没有账套记录时不存在历史段）
        if (split.HasValue && bgnDate <= split.Value)
        {
            DateTime histEnd = endDate < split.Value ? endDate : split.Value;
            foreach (var d in QueryDailyHistory(bgnDate, histEnd))
                rows.Add(ToRow(d, classInfos));
            hasHistory = true;
        }

        // 活动段：考勤日期 > Date_Bgn
        DateTime dailyBgn = (split.HasValue && bgnDate <= split.Value) ? split.Value.AddDays(1) : bgnDate;
        if (dailyBgn <= endDate)
        {
            foreach (var d in QueryDaily(dailyBgn, endDate))
                rows.Add(ToRow(d, classInfos));
            hasDaily = true;
        }

        source = hasHistory && hasDaily ? "mixed" : hasHistory ? "history" : hasDaily ? "daily" : "none";
        classes = CollectClasses(rows, classInfos);
        return rows.OrderBy(r => r.Date ?? string.Empty, StringComparer.Ordinal).ToList();
    }

    private static Dictionary<string, ClassInfo> CollectClasses(List<AttendanceRow> rows, Dictionary<string, ClassInfo> all)
    {
        var used = new Dictionary<string, ClassInfo>(StringComparer.OrdinalIgnoreCase);
        if (rows == null || all == null) return used;
        foreach (var r in rows)
        {
            if (string.IsNullOrEmpty(r.ClassNo) || used.ContainsKey(r.ClassNo)) continue;
            if (all.TryGetValue(r.ClassNo, out var info)) used[r.ClassNo] = info;
        }
        return used;
    }

    /// <summary>区间汇总，供 H5 / 小程序直接渲染，避免前端各算一套</summary>
    private static AttendanceSummary BuildSummary(List<AttendanceRow> rows)
    {
        var s = new AttendanceSummary();
        if (rows == null || rows.Count == 0) return s;
        foreach (var r in rows)
        {
            double must = r.Must ?? 0, fact = r.Fact ?? 0;
            if (must > 0) { s.MustDays++; s.MustHours += must; }
            if (fact > 0) { s.FactDays++; s.FactHours += fact; }
            if ((r.Absent ?? 0) > 0) s.AbsentCount++;
            if ((r.Late ?? 0) > 0) s.LateCount++;
            if ((r.Early ?? 0) > 0) s.EarlyCount++;
            if ((r.LeaveHour ?? 0) > 0) { s.LeaveCount++; s.LeaveHours += r.LeaveHour ?? 0; }
            if ((r.OtHour ?? 0) > 0) s.OtHours += r.OtHour ?? 0;
        }
        s.MustHours = Math.Round(s.MustHours, 2);
        s.FactHours = Math.Round(s.FactHours, 2);
        s.LeaveHours = Math.Round(s.LeaveHours, 2);
        s.OtHours = Math.Round(s.OtHours, 2);
        // 出勤率：实出勤天数 / 应出勤天数
        s.Rate = s.MustDays > 0 ? Math.Round(s.FactDays * 100.0 / s.MustDays, 1) : 0;
        return s;
    }

    /// <summary>接口统一失败体：HTTP 400 + { success:false, code, message }</summary>
    private IActionResult Fail(string code, string message)
        => BadRequest(new { success = false, code, message });

    /// <summary>账套分界日：Account_Flag=0 那条记录的 Date_Bgn；无记录或解析失败返回 null</summary>
    private DateTime? AccountSplitDate()
    {
        var acct = _db.KQ_Accounts.AsNoTracking().FirstOrDefault(a => a.Account_Flag == 0);
        if (acct == null) return null;
        return DateTime.TryParse(acct.Date_Bgn, out var d) ? d.Date : null;
    }

    /// <summary>活动表查询。Worktime_Date 是 varchar('yyyy-MM-dd')，直接比较才能命中索引</summary>
    private List<KQ_Daily> QueryDaily(DateTime bgn, DateTime end)
    {
        const string sql = "SELECT * FROM dbo.KQ_Daily "
            + "WHERE Person_ID = @p0 AND Worktime_Date >= @p1 AND Worktime_Date <= @p2 ORDER BY Worktime_Date";
        return _db.KQ_Dailies.FromSqlRaw(sql, Pid, Ymd(bgn), Ymd(end)).AsNoTracking().ToList();
    }

    /// <summary>历史表查询（表结构同 KQ_Daily）</summary>
    private List<KQ_DailyHistory> QueryDailyHistory(DateTime bgn, DateTime end)
    {
        const string sql = "SELECT * FROM dbo.KQ_DailyHistory "
            + "WHERE Person_ID = @p0 AND Worktime_Date >= @p1 AND Worktime_Date <= @p2 ORDER BY Worktime_Date";
        return _db.KQ_DailyHistories.FromSqlRaw(sql, Pid, Ymd(bgn), Ymd(end)).AsNoTracking().ToList();
    }

    /// <summary>区间内该人 KQ_Daily 已有记录的日期集合（用于逐日判断缺失）</summary>
    private HashSet<string> ExistingDates(DateTime bgn, DateTime end)
    {
        const string sql = "SELECT DISTINCT Worktime_Date AS Value FROM dbo.KQ_Daily "
            + "WHERE Person_ID = @p0 AND Worktime_Date >= @p1 AND Worktime_Date <= @p2";
        return new HashSet<string>(
            _db.Database.SqlQueryRaw<string>(sql, new SqlParameter("@p0", Pid),
                new SqlParameter("@p1", Ymd(bgn)), new SqlParameter("@p2", Ymd(end))).ToList(),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// 必要时补算每日考勤：只处理活动段、只算到今天；逐日检查缺失（旧实现只在整段行数为 0 时补算）。
    /// 最近 3 天有记录也按 CalcThrottleMinutes 节流重算；补算后仍无记录的日期按 AttemptedRetryMinutes 冷却。
    /// </summary>
    private bool EnsureCalculated(DateTime bgn, DateTime end)
    {
        DateTime today = DateTime.Today;
        if (bgn > today) return false;                    // 整段都是未来日期，无需计算
        DateTime calcEnd = end > today ? today : end;      // 未来日期不计算

        var guard = CalcGuards.GetOrAdd(Pid, _ => new CalcGuard());
        lock (guard.Sync)
        {
            PruneAttempted(guard);

            var existing = ExistingDates(bgn, calcEnd);
            var missing = new HashSet<string>(StringComparer.Ordinal);
            var targets = new SortedSet<DateTime>();
            DateTime recentFrom = today.AddDays(-3);
            bool throttled = guard.LastCalc != DateTime.MinValue
                && DateTime.Now - guard.LastCalc < TimeSpan.FromMinutes(CalcThrottleMinutes);

            for (DateTime d = bgn; d <= calcEnd; d = d.AddDays(1))
            {
                string key = Ymd(d);
                if (!existing.Contains(key))
                {
                    // 缺任何一天都要补算；仅当冷却期内已尝试过且仍无记录时跳过
                    missing.Add(key);
                    if (!AttemptedRecently(guard, key)) targets.Add(d);
                }
                else if (d >= recentFrom && !throttled)
                {
                    // 有记录但落在最近 3 天：打卡/审批数据可能仍在变化，按节流重算
                    targets.Add(d);
                }
            }

            if (targets.Count == 0) return false;

            int? oldTimeout = _db.Database.GetCommandTimeout();
            var calcSw = Stopwatch.StartNew();
            try
            {
                _db.Database.SetCommandTimeout(CalcCommandTimeout);
                foreach (var seg in ToSegments(targets))
                {
                    // 必须传 @PersonList：不传时过程会取 KQ_Person 全量人员，等于一个人查考勤、全公司重算
                    _db.Database.ExecuteSqlRaw("EXEC dbo.KQ_pd_CalcDaily @DateBgn, @DateEnd, @PersonList",
                        new SqlParameter("@DateBgn", Ymd(seg.Key)),
                        new SqlParameter("@DateEnd", Ymd(seg.Value)),
                        new SqlParameter("@PersonList", Pid.ToString()));

                    // 只登记真正缺失的日期：补算后仍无行说明当天本就没有考勤行，冷却期内不再重复触发
                    for (DateTime d = seg.Key; d <= seg.Value; d = d.AddDays(1))
                    {
                        string key = Ymd(d);
                        if (missing.Contains(key)) guard.AttemptedDays[key] = DateTime.Now;
                    }
                }
                guard.LastCalc = DateTime.Now;
                calcSw.Stop();
                _logger.LogInformation("KQ_pd_CalcDaily PersonId={Pid} bgn={Bgn} end={End} targetDays={Days} elapsed={Ms}ms",
                    Pid, Ymd(bgn), Ymd(calcEnd), targets.Count, calcSw.ElapsedMilliseconds);
                return true;
            }
            catch (Exception ex)
            {
                // 计算失败不影响已有记录读取；下次请求会重试（LastCalc 不更新）
                _logger.LogError(ex, "KQ_pd_CalcDaily 执行失败 PersonId={Pid} {Bgn}~{End}", Pid, Ymd(bgn), Ymd(calcEnd));
                return false;
            }
            finally
            {
                _db.Database.SetCommandTimeout(oldTimeout);
            }
        }
    }

    private static bool AttemptedRecently(CalcGuard guard, string key)
    {
        if (!guard.AttemptedDays.TryGetValue(key, out var at)) return false;
        if (DateTime.Now - at < TimeSpan.FromMinutes(AttemptedRetryMinutes)) return true;
        guard.AttemptedDays.Remove(key);
        return false;
    }

    /// <summary>清理过期的补算登记，避免字典随日期无限增长</summary>
    private static void PruneAttempted(CalcGuard guard)
    {
        if (guard.AttemptedDays.Count < 128) return;
        DateTime expire = DateTime.Now.AddMinutes(-AttemptedRetryMinutes * 2);
        foreach (var key in guard.AttemptedDays.Where(p => p.Value < expire).Select(p => p.Key).ToList())
            guard.AttemptedDays.Remove(key);
    }

    /// <summary>把待算日期合并成连续区间，避免月初缺一天却把整个区间重算一遍</summary>
    private static List<KeyValuePair<DateTime, DateTime>> ToSegments(SortedSet<DateTime> days)
    {
        var segments = new List<KeyValuePair<DateTime, DateTime>>();
        bool has = false;
        DateTime segBgn = DateTime.MinValue, segEnd = DateTime.MinValue;
        foreach (var d in days)
        {
            if (!has) { segBgn = segEnd = d; has = true; }
            else if (d == segEnd.AddDays(1)) segEnd = d;
            else
            {
                segments.Add(new KeyValuePair<DateTime, DateTime>(segBgn, segEnd));
                segBgn = segEnd = d;
            }
        }
        if (has) segments.Add(new KeyValuePair<DateTime, DateTime>(segBgn, segEnd));
        return segments;
    }

    private static readonly object ClassNameCacheLock = new();
    private static Dictionary<string, ClassInfo>? classInfoCache;
    private static DateTime classNameCacheAt = DateTime.MinValue;

    /// <summary>班次信息（名称 + 每段应打卡时间），10 分钟缓存；Class_No 重复时取第一条。</summary>
    private Dictionary<string, ClassInfo> ClassInfoDict()
    {
        if (classInfoCache != null && (DateTime.Now - classNameCacheAt).TotalMinutes < 10) return classInfoCache;
        lock (ClassNameCacheLock)
        {
            if (classInfoCache == null || (DateTime.Now - classNameCacheAt).TotalMinutes >= 10)
            {
                var map = new Dictionary<string, ClassInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (var c in _db.KQ_Classes.AsNoTracking().ToList())
                {
                    if (string.IsNullOrEmpty(c.Class_No) || map.ContainsKey(c.Class_No)) continue;
                    map[c.Class_No] = new ClassInfo
                    {
                        ClassNo = c.Class_No,
                        ClassName = c.Class_Name,
                        Segments = BuildClassSegments(c)
                    };
                }
                classInfoCache = map;
                classNameCacheAt = DateTime.Now;
            }
            return classInfoCache;
        }
    }

    /// <summary>
    /// 班次中「需要打卡」的时间段：SectN_InTime/OutTime 为空表示该段没排班；
    /// SectN_InCard/OutCard 为 0=否 1=是 2=可，免刷卡的方向不返回。
    /// </summary>
    private static List<ClassSegment> BuildClassSegments(KQ_Class c)
    {
        var list = new List<ClassSegment>();
        AddClassSegment(list, 1, c.Sect1_InTime, c.Sect1_OutTime, c.Sect1_InCard, c.Sect1_OutCard);
        AddClassSegment(list, 2, c.Sect2_InTime, c.Sect2_OutTime, c.Sect2_InCard, c.Sect2_OutCard);
        AddClassSegment(list, 3, c.Sect3_InTime, c.Sect3_OutTime, c.Sect3_InCard, c.Sect3_OutCard);
        AddClassSegment(list, 4, c.Sect4_InTime, c.Sect4_OutTime, c.Sect4_InCard, c.Sect4_OutCard);
        AddClassSegment(list, 5, c.Sect5_InTime, c.Sect5_OutTime, c.Sect5_InCard, c.Sect5_OutCard);
        return list;
    }

    private static void AddClassSegment(List<ClassSegment> list, int no,
        string? inTime, string? outTime, byte? inCard, byte? outCard)
    {
        if (string.IsNullOrEmpty(inTime) && string.IsNullOrEmpty(outTime)) return;   // 该段未排班
        // 只有配了时间且不是「否」的方向才需要打卡；标志为空按需要打卡处理
        bool needIn = !string.IsNullOrEmpty(inTime) && inCard != 0;
        bool needOut = !string.IsNullOrEmpty(outTime) && outCard != 0;
        if (!needIn && !needOut) return;   // 该段全免刷卡
        list.Add(new ClassSegment
        {
            No = no,
            InTime = inTime ?? "",
            OutTime = outTime ?? "",
            NeedIn = needIn,
            NeedOut = needOut
        });
    }

    /// <summary>考勤表日期为 varchar(10)，统一按 yyyy-MM-dd 传参</summary>
    private static string Ymd(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>严格解析 yyyy-MM-dd，避免 '2026-13-01' 这类值传到存储过程里 CAST 失败</summary>
    private static bool TryParseYmd(string? s, out DateTime d)
        => DateTime.TryParseExact((s ?? "").Trim(), "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out d);

    private DateTime ParseDate(string? s, DateTime def)
        => DateTime.TryParse(s, out var d) ? d : def;

    /// <summary>班次中的一段：应上班/下班时间 + 该方向是否需要打卡</summary>
    private sealed class ClassSegment
    {
        public int No { get; set; }
        public string InTime { get; set; } = "";
        public string OutTime { get; set; } = "";
        public bool NeedIn { get; set; }
        public bool NeedOut { get; set; }
    }

    /// <summary>班次定义（KQ_Class），按 Class_No 放在响应顶层，避免每行重复下发</summary>
    private sealed class ClassInfo
    {
        public string? ClassNo { get; set; }
        public string? ClassName { get; set; }
        public List<ClassSegment> Segments { get; set; } = new();
    }

    /// <summary>每日考勤行（KQ_Daily / KQ_DailyHistory 的公共投影）</summary>
    private sealed class AttendanceRow
    {
        public string? Date { get; set; }
        public string? Week { get; set; }
        public string? ClassNo { get; set; }
        public string? ClassName { get; set; }
        public string? ClassData { get; set; }
        public string? In1 { get; set; }
        public string? Out1 { get; set; }
        public string? In2 { get; set; }
        public string? Out2 { get; set; }
        public string? In3 { get; set; }
        public string? Out3 { get; set; }
        public string? In4 { get; set; }
        public string? Out4 { get; set; }
        public string? In5 { get; set; }
        public string? Out5 { get; set; }
        public double? Must { get; set; }
        public double? Fact { get; set; }
        public double? Absent { get; set; }
        public string? Reason { get; set; }
        public double? LeaveHour { get; set; }
        public string? LeaveName { get; set; }
        public double? OtHour { get; set; }
        public double? Late { get; set; }
        public double? Early { get; set; }
        public string? Brush { get; set; }
        public int Segments { get; set; }   // 当天实际打卡段数，前端据此决定是否展开多段
    }

    /// <summary>区间汇总（应出勤/实出勤/迟到/早退/缺卡/请假/加班/出勤率）</summary>
    private sealed class AttendanceSummary
    {
        public int MustDays { get; set; }
        public int FactDays { get; set; }
        public int LateCount { get; set; }
        public int EarlyCount { get; set; }
        public int AbsentCount { get; set; }
        public int LeaveCount { get; set; }
        public double LeaveHours { get; set; }
        public double OtHours { get; set; }
        public double MustHours { get; set; }
        public double FactHours { get; set; }
        public double Rate { get; set; }
    }

    private AttendanceRow ToRow(KQ_Daily d, Dictionary<string, ClassInfo> classes)
    {
        return BuildRow(classes, d.Worktime_Date, d.OfWeek, d.Class_No, d.Class_Data,
            d.Sect1_InFact, d.Sect1_OutFact, d.Sect2_InFact, d.Sect2_OutFact,
            d.Sect3_InFact, d.Sect3_OutFact, d.Sect4_InFact, d.Sect4_OutFact,
            d.Sect5_InFact, d.Sect5_OutFact,
            d.Must_Work, d.Fact_Work, d.Absent_Work, d.Reason, d.Leave_Hour, d.Leave_Name,
            d.Overtime_Hour, d.Late_Minute, d.Early_Minute, d.Brush_Data);
    }

    private AttendanceRow ToRow(KQ_DailyHistory d, Dictionary<string, ClassInfo> classes)
    {
        return BuildRow(classes, d.Worktime_Date, d.OfWeek, d.Class_No, d.Class_Data,
            d.Sect1_InFact, d.Sect1_OutFact, d.Sect2_InFact, d.Sect2_OutFact,
            d.Sect3_InFact, d.Sect3_OutFact, d.Sect4_InFact, d.Sect4_OutFact,
            d.Sect5_InFact, d.Sect5_OutFact,
            d.Must_Work, d.Fact_Work, d.Absent_Work, d.Reason, d.Leave_Hour, d.Leave_Name,
            d.Overtime_Hour, d.Late_Minute, d.Early_Minute, d.Brush_Data);
    }

    /// <summary>两张考勤表字段完全一致，抽出公共构造，避免字段映射写两遍走样</summary>
    private static AttendanceRow BuildRow(Dictionary<string, ClassInfo> classes,
        string? date, string? week, string? classNo, string? classData,
        string? in1, string? out1, string? in2, string? out2,
        string? in3, string? out3, string? in4, string? out4, string? in5, string? out5,
        double? must, double? fact, double? absent, string? reason, double? leaveHour, string? leaveName,
        double? otHour, double? late, double? early, string? brush)
    {
        string className = classNo ?? "";
        if (classNo != null && classes != null && classes.TryGetValue(classNo, out var info)
            && !string.IsNullOrEmpty(info.ClassName))
            className = info.ClassName!;
        int segments = 0;
        if (!string.IsNullOrEmpty(in1) || !string.IsNullOrEmpty(out1)) segments = 1;
        if (!string.IsNullOrEmpty(in2) || !string.IsNullOrEmpty(out2)) segments = 2;
        if (!string.IsNullOrEmpty(in3) || !string.IsNullOrEmpty(out3)) segments = 3;
        if (!string.IsNullOrEmpty(in4) || !string.IsNullOrEmpty(out4)) segments = 4;
        if (!string.IsNullOrEmpty(in5) || !string.IsNullOrEmpty(out5)) segments = 5;

        return new AttendanceRow
        {
            Date = date, Week = week, ClassNo = classNo, ClassName = className, ClassData = classData,
            In1 = in1, Out1 = out1, In2 = in2, Out2 = out2,
            In3 = in3, Out3 = out3, In4 = in4, Out4 = out4, In5 = in5, Out5 = out5,
            Must = must, Fact = fact, Absent = absent, Reason = reason,
            LeaveHour = leaveHour, LeaveName = leaveName, OtHour = otHour,
            Late = late, Early = early, Brush = brush, Segments = segments
        };
    }

    // ================= 请假（时间段） =================

    [HttpGet("leave/kinds")]
    public IActionResult LeaveKinds()
        => Ok(_db.KQ_LeaveKinds.AsNoTracking().Select(k => new { k.Kind_No, k.Kind_Name }).ToList());

    /// <summary>已批准的请假记录（KQ_Leave，审批通过后才会有数据）</summary>
    [HttpGet("leave/list")]
    public IActionResult LeaveList()
    {
        var kinds = _db.KQ_LeaveKinds.AsNoTracking().ToDictionary(k => k.Kind_No, k => k.Kind_Name);
        return Ok(_db.KQ_Leaves.AsNoTracking().Where(l => l.Person_ID == Pid).OrderByDescending(l => l.Leave_Date)
            .ToList().Select(l => new
            {
                id = l.ID_KEY,
                kind = l.Kind_No,
                // 类型名称：原来只返回 Kind_No（编码），前端显示成「请假（01）」
                kindName = l.Kind_No != null && kinds.ContainsKey(l.Kind_No) ? kinds[l.Kind_No] : (l.Kind_No ?? ""),
                date = l.Leave_Date,
                timeBgn = l.Leave_TimeBgn,
                timeEnd = l.Leave_TimeEnd,
                hour = l.Leave_Hour,
                remark = l.Remark
            }).ToList());
    }

    /// <summary>
    /// 请假申请：写入审批表 WX_KQ_Apply（待审批）。审批通过后才写 KQ_Leave，
    /// 避免「提交即生效」把未审批的假算进考勤；保留原路径以兼容旧版客户端。
    /// </summary>
    [HttpPost("leave/submit")]
    public IActionResult LeaveSubmit([FromBody] LeaveSubmitReq req)
    {
        if (req == null) return Ok(new { success = false, msg = "参数错误" });
        string? applyNo = KqApplyHelper.SubmitLeave(Pid, req.kindNo, req.timeBgn, req.timeEnd, req.remark,
            string.IsNullOrEmpty(req.scene) ? "h5" : req.scene, out var msg);
        if (applyNo == null) return Ok(new { success = false, msg });
        return Ok(new { success = true, applyNo });
    }
}

public class LeaveSubmitReq
{
    public string? kindNo { get; set; }
    public string? timeBgn { get; set; }
    public string? timeEnd { get; set; }
    public string? remark { get; set; }
    public string? scene { get; set; }
}

public class NormalRecRow
{
    public int ID_KEY { get; set; }
    public DateTime BrushCard_Time { get; set; }
    public string? Ctrl_No { get; set; }
    public int? Door_No { get; set; }
    public string? Door_Name { get; set; }
    public string? Card_No { get; set; }
    public int? Event_Code { get; set; }
    public bool Is_InDoor { get; set; }
}

public class AbnormRecRow
{
    public int ID_KEY { get; set; }
    public DateTime BrushCard_Time { get; set; }
    public string? Ctrl_No { get; set; }
    public int? Door_No { get; set; }
    public string? Door_Name { get; set; }
    public string? Card_No { get; set; }
    public int? Event_Code { get; set; }
    public bool Is_InDoor { get; set; }
}
