using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using PsnMgrService.Controllers.SystemAdmin;

namespace PsnMgrService.Services;

/// <summary>
/// 同步任务执行器：轮询 BS_SyncLog 中 Status=0 的待执行记录，抢占后按任务与端点类型执行，
/// 回写状态、耗时、请求/响应摘要，并同步 BS_MaintJob 进度与步骤日志。
///
/// 状态约定（与 SyncLogsController 的映射一致）：
/// 0=待执行，4=执行中，1=成功，2=失败，3=部分成功。
///
/// 多实例部署（例如 5000 与 5187 两个进程连同一库）时，靠
/// <c>UPDATE ... WHERE ID_KEY = @id AND Status = 0</c> 的原子性抢占，只有一个实例能执行。
/// </summary>
public sealed class SyncTaskRunner : BackgroundService
{
    private const int StatusPending = 0;
    private const int StatusSuccess = 1;
    private const int StatusFailed = 2;
    private const int StatusPartial = 3;
    private const int StatusRunning = 4;

    /// <summary>共享 HttpClient；单次请求超时由任务/端点配置控制，这里禁用全局超时。</summary>
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    private readonly ILogger<SyncTaskRunner> _logger;
    private readonly IConfiguration _config;

    public SyncTaskRunner(ILogger<SyncTaskRunner> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue("Sync:RunnerEnabled", true))
        {
            _logger.LogInformation("同步任务执行器已禁用（Sync:RunnerEnabled=false）");
            return;
        }

        var interval = Math.Clamp(_config.GetValue("Sync:RunnerIntervalSeconds", 5), 2, 300);
        var batch = Math.Clamp(_config.GetValue("Sync:RunnerBatch", 5), 1, 50);
        var startupDelay = Math.Clamp(_config.GetValue("Sync:RunnerStartupDelaySeconds", 10), 0, 600);

        _logger.LogInformation(
            "同步任务执行器启动：启动延迟 {Delay}s，每 {Interval}s 扫描，单轮最多 {Batch} 条",
            startupDelay, interval, batch);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(startupDelay), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Run(() => RunOnce(batch, stoppingToken), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "同步任务执行器本轮异常，{Interval}s 后重试", interval);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("同步任务执行器已停止");
    }

    /// <summary>扫描一轮：取待执行记录逐条抢占并执行。</summary>
    private void RunOnce(int batch, CancellationToken ct)
    {
        using var db = SqlAccess.Open();

        // 延迟 3 秒再捡，避免与刚提交的事务抢同一条记录
        var rows = db.Query<PendingRow>(
            @"SELECT TOP (@n) ID_KEY AS Id, Task_Code AS TaskCode, Sync_Type AS SyncType,
                     Direction AS Direction, Target AS Target, Target_Key AS TargetKey,
                     Retry_Count AS RetryCount, Trace_ID AS TraceId
                FROM dbo.BS_SyncLog
               WHERE Status = 0 AND Create_Time <= DATEADD(SECOND, -3, GETDATE())
               ORDER BY ID_KEY",
            SqlSession.P("@n", batch));

        foreach (var row in rows)
        {
            if (ct.IsCancellationRequested) break;

            // 原子抢占：只有把 0 改成 4 成功的那一个实例才执行
            var claimed = db.Exec(
                "UPDATE dbo.BS_SyncLog SET Status = 4, Message = N'执行中', Finish_Time = NULL WHERE ID_KEY = @id AND Status = 0",
                SqlSession.P("@id", row.Id));
            if (claimed == 0) continue;

            ExecuteOne(db, row, ct);
        }
    }

    private void ExecuteOne(SqlSession db, PendingRow row, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var status = StatusFailed;
        var message = "";
        string? requestBody = null;
        string? responseBody = null;
        var taskCode = (row.TaskCode ?? "").Trim();

        try
        {
            if (taskCode.Length == 0)
            {
                message = "记录缺少任务编码，无法执行";
            }
            else if (string.Equals(taskCode, "LOG_ARCHIVE", StringComparison.OrdinalIgnoreCase))
            {
                var archived = LogArchiver.Run(db);
                status = StatusSuccess;
                message = $"归档 {archived} 行";
                responseBody = $"{{\"archived\":{archived}}}";
            }
            else
            {
                var task = db.QuerySingle<TaskRow>(
                    @"SELECT Task_Code AS TaskCode, Task_Type AS TaskType, Endpoint_Code AS EndpointCode,
                             Direction AS Direction, Batch_Size AS BatchSize, Timeout_Sec AS TimeoutSec
                        FROM dbo.BS_SyncTask WHERE Task_Code = @c",
                    SqlSession.P("@c", taskCode));

                if (task == null)
                {
                    message = $"同步任务 {taskCode} 已不存在";
                }
                else
                {
                    EndpointRow? endpoint = null;
                    if (!string.IsNullOrWhiteSpace(task.EndpointCode))
                    {
                        endpoint = db.QuerySingle<EndpointRow>(
                            @"SELECT Endpoint_Code AS EndpointCode, Endpoint_Type AS EndpointType, Base_Url AS BaseUrl,
                                     Is_Enabled AS IsEnabled, Timeout_Sec AS TimeoutSec
                                FROM dbo.BS_IntegrationEndpoint WHERE Endpoint_Code = @c",
                            SqlSession.P("@c", task.EndpointCode));
                    }

                    (status, message, requestBody, responseBody) = Dispatch(db, row, task, endpoint, ct);
                }
            }
        }
        catch (Exception ex)
        {
            status = StatusFailed;
            message = "执行异常：" + ex.Message;
            _logger.LogWarning(ex, "同步任务 {TaskCode}（记录 {Id}）执行异常", taskCode, row.Id);
        }

        stopwatch.Stop();
        var elapsed = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue);

        try
        {
            db.Exec(
                @"UPDATE dbo.BS_SyncLog
                     SET Status = @st, Message = @msg, Request_Body = @req, Response_Body = @resp,
                         Elapsed = @elapsed, Finish_Time = GETDATE()
                   WHERE ID_KEY = @id",
                SqlSession.P("@st", status), SqlSession.P("@msg", Cut(message, 1000)),
                SqlSession.P("@req", (object?)requestBody), SqlSession.P("@resp", (object?)responseBody),
                SqlSession.P("@elapsed", elapsed), SqlSession.P("@id", row.Id));

            if (taskCode.Length > 0)
            {
                db.Exec(
                    @"UPDATE dbo.BS_SyncTask
                         SET Last_Run_Time = GETDATE(), Last_Status = @st, Last_Message = @msg, Update_Time = GETDATE()
                       WHERE Task_Code = @c",
                    SqlSession.P("@st", status), SqlSession.P("@msg", Cut(message, 500)),
                    SqlSession.P("@c", taskCode));
            }

            UpdateJob(db, row.Id, status, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步任务 {TaskCode}（记录 {Id}）回写结果失败", taskCode, row.Id);
        }
    }

    /// <summary>按任务与端点类型分派。返回状态、消息、请求与响应摘要。</summary>
    private static (int status, string message, string? request, string? response) Dispatch(
        SqlSession db, PendingRow row, TaskRow task, EndpointRow? endpoint, CancellationToken ct)
    {
        if (endpoint == null)
            return (StatusFailed, "任务未绑定集成端点，无法执行", null, null);
        if (!endpoint.IsEnabled)
            return (StatusFailed, $"端点 {endpoint.EndpointCode} 已停用，请先启用", null, null);

        var type = (endpoint.EndpointType ?? "").Trim().ToLowerInvariant();
        if (type == "database")
        {
            db.ScalarInt("SELECT 1");
            return (StatusSuccess, $"数据库端点 {endpoint.EndpointCode} 连通，无需外部调用", null, null);
        }

        if (string.IsNullOrWhiteSpace(endpoint.BaseUrl))
            return (StatusFailed, $"端点 {endpoint.EndpointCode} 未配置地址，请在“接口与集成”中填写 Base_Url", null, null);

        var taskCode = (row.TaskCode ?? "").Trim();
        var direction = (row.Direction ?? task.Direction ?? "down").Trim().ToLowerInvariant();
        var timeout = Math.Clamp(task.TimeoutSec > 0 ? task.TimeoutSec : endpoint.TimeoutSec, 5, 300);
        var url = BuildUrl(endpoint.BaseUrl!, taskCode, direction, row.TargetKey, row.Id);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeout));

        try
        {
            if (direction == "up")
            {
                using var response = Http.GetAsync(url, cts.Token).GetAwaiter().GetResult();
                var body = ReadBody(response);
                return (response.IsSuccessStatusCode ? StatusSuccess : StatusFailed,
                    $"HTTP {(int)response.StatusCode} {Cut(response.ReasonPhrase, 100)}", url, body);
            }

            var payload = JsonSerializer.Serialize(new
            {
                taskCode,
                direction,
                batchSize = task.BatchSize,
                targetKey = row.TargetKey,
                traceId = row.TraceId,
                syncId = row.Id,
            });

            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            request.Headers.TryAddWithoutValidation("X-Task-Code", taskCode);
            using var postResponse = Http.SendAsync(request, cts.Token).GetAwaiter().GetResult();
            var postBody = ReadBody(postResponse);
            return (postResponse.IsSuccessStatusCode ? StatusSuccess : StatusFailed,
                $"HTTP {(int)postResponse.StatusCode} {Cut(postResponse.ReasonPhrase, 100)}", payload, postBody);
        }
        catch (OperationCanceledException)
        {
            return (StatusFailed, $"请求超时（{timeout}s）", null, null);
        }
        catch (Exception ex)
        {
            return (StatusFailed, "请求失败：" + ex.Message, null, null);
        }
    }

    /// <summary>把执行结果回写到由“上传与下载”创建的作业，并追加步骤日志。</summary>
    private static void UpdateJob(SqlSession db, long syncId, int status, string message)
    {
        var jobId = db.ScalarInt(
            "SELECT TOP 1 Job_ID FROM dbo.BS_MaintJob WHERE Job_Type = 'transfer' AND Params_Json LIKE @p ORDER BY Job_ID DESC",
            SqlSession.P("@p", $"%\"syncLogId\":{syncId}%"));
        if (jobId <= 0) return;

        // 作业状态：2=成功，3=失败（部分成功按成功记录，原因写在消息里）
        var jobStatus = status == StatusSuccess || status == StatusPartial ? 2 : 3;
        db.Exec(
            @"UPDATE dbo.BS_MaintJob
                 SET Status = @s, Progress = 100, Finish_Time = GETDATE(), Message = @m
               WHERE Job_ID = @id",
            SqlSession.P("@s", jobStatus), SqlSession.P("@m", Cut(message, 1000)), SqlSession.P("@id", jobId));

        db.Exec(
            "INSERT INTO dbo.BS_MaintLog (Job_ID, Step, Message, Affected, Log_Time) VALUES (@id, @step, @msg, NULL, GETDATE())",
            SqlSession.P("@id", jobId),
            SqlSession.P("@step", status == StatusSuccess || status == StatusPartial ? "EXECUTE" : "FAILED"),
            SqlSession.P("@msg", Cut(message, 1000)));
    }

    private static string BuildUrl(string baseUrl, string taskCode, string direction, string? targetKey, long syncId)
    {
        var separator = baseUrl.Contains('?') ? '&' : '?';
        var query = new StringBuilder();
        query.Append("taskCode=").Append(Uri.EscapeDataString(taskCode));
        query.Append("&direction=").Append(Uri.EscapeDataString(direction));
        query.Append("&syncId=").Append(syncId.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(targetKey))
            query.Append("&targetKey=").Append(Uri.EscapeDataString(targetKey));
        return baseUrl + separator + query;
    }

    private static string ReadBody(HttpResponseMessage response)
    {
        try
        {
            var text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= 4000 ? text : text[..4000] + "...(已截断)";
        }
        catch
        {
            return "";
        }
    }

    private static string Cut(string? text, int max)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= max ? text : text[..max];
    }

    private sealed class PendingRow
    {
        public long Id { get; set; }
        public string? TaskCode { get; set; }
        public string? SyncType { get; set; }
        public string? Direction { get; set; }
        public string? Target { get; set; }
        public string? TargetKey { get; set; }
        public int RetryCount { get; set; }
        public string? TraceId { get; set; }
    }

    private sealed class TaskRow
    {
        public string TaskCode { get; set; } = "";
        public string? TaskType { get; set; }
        public string? EndpointCode { get; set; }
        public string? Direction { get; set; }
        public int BatchSize { get; set; }
        public int TimeoutSec { get; set; }
    }

    private sealed class EndpointRow
    {
        public string EndpointCode { get; set; } = "";
        public string? EndpointType { get; set; }
        public string? BaseUrl { get; set; }
        public bool IsEnabled { get; set; }
        public int TimeoutSec { get; set; }
    }
}
