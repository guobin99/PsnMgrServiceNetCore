using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

/* =========================================================================
 * 系统配置与安全审计：接口传输结构。
 * 属性名一律小驼峰，与前端 TypeScript 定义一一对应；SQL 查询用同名别名绑定。
 * ========================================================================= */

/* ---------------- 系统参数 ---------------- */

public class ParamItemDto
{
    public string code { get; set; } = "";
    public string name { get; set; } = "";
    public string? value { get; set; }
    public string? defaultValue { get; set; }
    public string type { get; set; } = "string";
    public string? options { get; set; }
    public string? minValue { get; set; }
    public string? maxValue { get; set; }
    public string? groupName { get; set; }
    public bool restartRequired { get; set; }
    public bool readOnly { get; set; }
    public string? remark { get; set; }
    public DateTime? updatedTime { get; set; }
}

public class ParamScopeNodeDto
{
    public string scope { get; set; } = "";
    public string scopeName { get; set; } = "";
    public List<ParamGroupNodeDto> groups { get; set; } = new();
}

public class ParamGroupNodeDto
{
    public string groupName { get; set; } = "";
    public int count { get; set; }
}

public class ParamSaveItem
{
    public string? ParamCode { get; set; }
    public string? Value { get; set; }
    public string? OldValue { get; set; }
}

public class ParamSaveRequest
{
    public string? Scope { get; set; }
    public List<ParamSaveItem> Items { get; set; } = new();
}

public class ParamResetRequest
{
    public string? Scope { get; set; }
    public List<string> Codes { get; set; } = new();
}

public class ParamBackupDto
{
    public int backupId { get; set; }
    public string scope { get; set; } = "";
    public string backupName { get; set; } = "";
    public int itemCount { get; set; }
    public string? hash { get; set; }
    public string? createUser { get; set; }
    public DateTime createTime { get; set; }
    public string? remark { get; set; }
}

/* ---------------- 数据字典 ---------------- */

public class DictTableDto
{
    public string tableName { get; set; } = "";
    public string? tableChsName { get; set; }
    public int itemCount { get; set; }
}

public class DictItemDto
{
    public int id { get; set; }
    public string tableName { get; set; } = "";
    public string? dictId { get; set; }
    public string? dictValue { get; set; }
    public bool sysCol { get; set; }
    public string? remark { get; set; }
}

public class DictItemSaveRequest
{
    public string? TableName { get; set; }
    public string? Dict_Id { get; set; }
    public string? Dict_Value { get; set; }
    public bool? Is_SysCol { get; set; }
    public string? Remark { get; set; }
    public string? OldDict_Id { get; set; }
    public string? OldDict_Value { get; set; }
}

public class DictMetaFieldDto
{
    public int id { get; set; }
    public string tableName { get; set; } = "";
    public string fieldName { get; set; } = "";
    public string? fieldNote { get; set; }
    public int? fieldNo { get; set; }
    public bool isPk { get; set; }
    public string? fieldType { get; set; }
    public int? fieldLength { get; set; }
    public string? baseTable { get; set; }
}

public class DictMetaColumnDto
{
    public int id { get; set; }
    public string? gridName { get; set; }
    public string? fieldName { get; set; }
    public string? displayName { get; set; }
    public int? displayLen { get; set; }
    public int? fieldIndex { get; set; }
    public bool isShow { get; set; }
    public bool isSum { get; set; }
}

public class ConsistencyItemDto
{
    public string issueType { get; set; } = "";
    public string tableName { get; set; } = "";
    public string? fieldName { get; set; }
    public string? metaValue { get; set; }
    public string? dbValue { get; set; }
    public string? message { get; set; }
}

/* ---------------- 接口与集成 ---------------- */

public class EndpointDto
{
    public int id { get; set; }
    public string endpointCode { get; set; } = "";
    public string endpointName { get; set; } = "";
    public string endpointType { get; set; } = "";
    public string? baseUrl { get; set; }
    public string? authMode { get; set; }
    public string? configJson { get; set; }
    public bool hasSecret { get; set; }
    public DateTime? secretUpdatedTime { get; set; }
    public int? timeoutSec { get; set; }
    public int? retryCount { get; set; }
    public bool isEnabled { get; set; }
    public byte healthStatus { get; set; }
    public DateTime? lastCheckTime { get; set; }
    public string? lastCheckMessage { get; set; }
    public int? sortNo { get; set; }
    public int taskCount { get; set; }
    public int secretHistoryCount { get; set; }
}

public class EndpointSaveRequest
{
    public string? Endpoint_Name { get; set; }
    public string? Endpoint_Type { get; set; }
    public string? Base_Url { get; set; }
    public string? Auth_Mode { get; set; }
    public string? Config_Json { get; set; }
    public int? Timeout_Sec { get; set; }
    public int? Retry_Count { get; set; }
    public bool? Is_Enabled { get; set; }
    public int? Sort_No { get; set; }
    public string? Endpoint_Code { get; set; }
}

public class EndpointSecretRequest
{
    public string? Secret { get; set; }
    public string? Secret_Confirm { get; set; }
    public string? Reason { get; set; }
}

public class SyncTaskDto
{
    public int taskId { get; set; }
    public string taskCode { get; set; } = "";
    public string taskName { get; set; } = "";
    public string taskType { get; set; } = "";
    public string? endpointCode { get; set; }
    public string direction { get; set; } = "down";
    public string? cronExpr { get; set; }
    public int? batchSize { get; set; }
    public int? timeoutSec { get; set; }
    public bool isEnabled { get; set; }
    public DateTime? lastRunTime { get; set; }
    public byte? lastStatus { get; set; }
    public string? lastMessage { get; set; }
    public DateTime? nextRunTime { get; set; }
}

/* ---------------- 数据维护 ---------------- */

public class MaintJobDto
{
    public int jobId { get; set; }
    public string jobType { get; set; } = "";
    public string jobName { get; set; } = "";
    public string? paramsJson { get; set; }
    public byte status { get; set; }
    public int progress { get; set; }
    public string? message { get; set; }
    public int? backupJobId { get; set; }
    public string? createUser { get; set; }
    public DateTime createTime { get; set; }
    public DateTime? startTime { get; set; }
    public DateTime? finishTime { get; set; }
    public string? traceId { get; set; }
}

public class MaintStepDto
{
    public long idKey { get; set; }
    public int jobId { get; set; }
    public string? step { get; set; }
    public string? message { get; set; }
    public int? affected { get; set; }
    public DateTime logTime { get; set; }
}

public class FragmentationDto
{
    public string schemaName { get; set; } = "";
    public string tableName { get; set; } = "";
    public string indexName { get; set; } = "";
    public double avgFragmentation { get; set; }
    public long pageCount { get; set; }
}

public class CleanupTableDto
{
    public string tableName { get; set; } = "";
    public string dateField { get; set; } = "";
    public string? historyTable { get; set; }
    public int clearIndex { get; set; }
}

/* ---------------- 黑名单 ---------------- */

public class BlacklistItemDto
{
    public int id { get; set; }
    public int? personId { get; set; }
    public string? cardNo { get; set; }
    public string? personNo { get; set; }
    public string? personName { get; set; }
    public string? deptName { get; set; }
    public DateTime? delDate { get; set; }
    public bool delSign { get; set; }
    public string? remark { get; set; }
    public string? cardState { get; set; }
    public int retainDays { get; set; }
    public DateTime? releaseableTime { get; set; }
    public bool releaseable { get; set; }
    public string? addUser { get; set; }
    public string source { get; set; } = "manual";
}

public class BlacklistAddRequest
{
    public string? Card_No { get; set; }
    public int? Person_ID { get; set; }
    public string? Reason { get; set; }
    public bool? Del_Sign { get; set; }
    public bool? SyncNow { get; set; }
}

public class BlacklistReleaseRequest
{
    public string? Reason { get; set; }
}

public class CardLookupDto
{
    public string cardNo { get; set; } = "";
    public int? personId { get; set; }
    public string? personNo { get; set; }
    public string? personName { get; set; }
    public string? deptName { get; set; }
    public string? cardState { get; set; }
    public DateTime? validDate { get; set; }
    public bool exists { get; set; }
    public bool inBlacklist { get; set; }
    public int retainDays { get; set; }
}

/* ---------------- 日志 ---------------- */

public class SystemLogDto
{
    public string source { get; set; } = "CS";
    public long id { get; set; }
    public DateTime time { get; set; }
    public string? userName { get; set; }
    public string? computer { get; set; }
    public string? systemNo { get; set; }
    public string? eventDesc { get; set; }
    public string? traceId { get; set; }
    public string? module { get; set; }
    public string? action { get; set; }
}

public class LoginLogDto
{
    public string source { get; set; } = "BS";
    public long id { get; set; }
    public DateTime time { get; set; }
    public string? userNo { get; set; }
    public string? userName { get; set; }
    public string? loginType { get; set; }
    public byte result { get; set; }
    public string? failReason { get; set; }
    public string? ip { get; set; }
    public string? userAgent { get; set; }
    public DateTime? logoutTime { get; set; }
    public string? sessionId { get; set; }
    public int repeatCount { get; set; }
}

public class OperLogDto
{
    public string source { get; set; } = "BS";
    public long id { get; set; }
    public DateTime time { get; set; }
    public string? userNo { get; set; }
    public string? userName { get; set; }
    public string? module { get; set; }
    public string? action { get; set; }
    public string? target { get; set; }
    public string? targetKey { get; set; }
    public string? beforeValue { get; set; }
    public string? afterValue { get; set; }
    public byte result { get; set; }
    public string? message { get; set; }
    public string? ip { get; set; }
    public string? traceId { get; set; }
}

public class SyncLogDto
{
    public string source { get; set; } = "BS";
    public string? sourceLabel { get; set; }
    public long id { get; set; }
    public DateTime time { get; set; }
    public string? taskCode { get; set; }
    public string? syncType { get; set; }
    public string? direction { get; set; }
    public string? target { get; set; }
    public string? targetKey { get; set; }
    public string status { get; set; } = "unknown";
    public int? elapsed { get; set; }
    public string? message { get; set; }
    public int retryCount { get; set; }
    public string? traceId { get; set; }
    public string? requestBody { get; set; }
    public string? responseBody { get; set; }
}

/* ---------------- 参数导入 / 备份请求 ---------------- */

public class ParamSnapshotItem
{
    public string? code { get; set; }
    public string? name { get; set; }
    public string? value { get; set; }
}

public class ParamBackupRequest
{
    public string? Scope { get; set; }
    public string? Name { get; set; }
    public string? Remark { get; set; }
}

public class ParamImportRequest
{
    public string? Scope { get; set; }
    public List<ParamSnapshotItem> Items { get; set; } = new();
}

public class DictImportItem
{
    public string? dictId { get; set; }
    public string? dictValue { get; set; }
}

public class DictImportRequest
{
    public string? TableName { get; set; }
    public bool Overwrite { get; set; }
    public List<DictImportItem> Items { get; set; } = new();
}

public class DictMetaSyncRequest
{
    public string? TableName { get; set; }
}

public class EndpointToggleRequest
{
    public bool? Enabled { get; set; }
}

public class SyncTaskSaveRequest
{
    public string? Cron_Expr { get; set; }
    public int? Batch_Size { get; set; }
    public int? Timeout_Sec { get; set; }
    public bool? Is_Enabled { get; set; }
}

/* ---------------- 维护作业请求 ---------------- */

public class MaintModeRequest
{
    public bool? Enabled { get; set; }
    public int? Hours { get; set; }
}

public class BackupRequest
{
    public string? BackupType { get; set; }
    public string? TargetPath { get; set; }
    public bool? Compression { get; set; }
    public bool? Verify { get; set; }
}

public class MaintObject
{
    public string? TableName { get; set; }
    public string? IndexName { get; set; }
}

public class MaintObjectRequest
{
    public List<MaintObject> Items { get; set; } = new();
    public List<string> Tables { get; set; } = new();
}

public class CleanupRequest
{
    public string? CutoffDate { get; set; }
    public string? ConfirmPhrase { get; set; }
    public bool? BackupDone { get; set; }
    public bool? CsExited { get; set; }
    public bool? Irreversible { get; set; }
}

public class TransferRequest
{
    public string? TaskCode { get; set; }
    public string? Direction { get; set; }
    public string? Target { get; set; }
    public string? TargetKey { get; set; }
}

public class BlacklistImportItem
{
    public string? cardNo { get; set; }
    public string? reason { get; set; }
}

public class BlacklistImportRequest
{
    public List<BlacklistImportItem> Items { get; set; } = new();
}

/// <summary>日志统计用的键值行。</summary>
public class CountRow
{
    public string? keyName { get; set; }
    public int cnt { get; set; }
}
