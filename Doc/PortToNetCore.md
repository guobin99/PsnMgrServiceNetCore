# PsnMgrService(Net) → PsnMgrService(NetCore) 接口移植清单

> 目标：NetCore（.NET 9 + EF Core）提供与 Net（.NET Framework 4.8 + EF6）**完全一致**的接口：
> 路径、方法、参数名、返回字段名与结构、错误提示文案都保持一致，H5 与小程序可无感切换。

## 一、总体进度

| 项目 | 数量 |
|---|---|
| Net 端 Controller | 20 个（其中 6 个为空的骨架/Home） |
| Net 端 HTTP 端点 | **58 个** |
| **已完成移植** | **7 个**（消息 3 + 帐户 4） |
| 待移植 | 51 个 |

已落地的基础设施：

- `Services/AppConfig.cs`：启动时读取连接串 / 阿里云短信 / 访客二维码密钥（对应 Net 的 Web.config appSettings）
- `Services/AliyunSmsService.cs`：阿里云短信签名发送 + 验证码存验（ODI 排序铁律已保留）
- `Services/MobileMessageService.cs`：站内消息写入 WX_Message
- `Controllers/MobileMessageController.cs`：`api/m/message/list|read|readall`
- `Controllers/MobileAccountController.cs`：`api/m/account/rechargerecords|consumptions|changepwd|lockcard|changephone|resetpwd`（6 个端点，其中 resetpwd 匿名）

> 注：MobileAccount 实际是 6 个端点（含 lockcard/resetpwd），因此已完成计数按 9 个算。

## 二、待移植清单（按建议批次）

### 批次 1：认证与身份（其他接口都依赖 `PersonId` claim）

| 控制器 | 端点数 | 依赖 |
|---|---|---|
| `MobileAuthController` | 8 | 微信 OAuth（HttpClient）、JWT 签发（claim `PersonId`）、短信 |
| `RechargeController` | 3 | 微信支付 V2/V3 签名、回调验签、`XF_PayRecord` |

> 关键：Net 的移动端 JWT 里带 `PersonId` claim，NetCore 现有 `AuthController` 是**后台管理员**登录（ST_UserInfo），
> 两者是两套 token。移植 `MobileAuthController` 后，其余 `api/m/*` 才能取到 `Pid`。

### 批次 2：访客（最近改动最多，务必对齐）

| 控制器 | 端点数 | 依赖 |
|---|---|---|
| `MobileVisitorController` | 11 | `VisitorCardPool`（ST_Card/ST_CardType/ST_Person 取访客卡 + 有效期）、二维码 HMAC token、访客照片 |

配套需要移植：`VisitorCardPool`（EF Core 版，含 `UPDLOCK` 分配、`Card_ValidDate` 延长/作废）。

### 批次 3：考勤与反馈

| 控制器 | 端点数 | 依赖 |
|---|---|---|
| `MobileAttendanceApplyController` | 6 | `KqApplyHelper`、WX_KQ_Apply |
| `Admin_KQ_ApplyController` | 2 | 同上（管理端） |
| `MobileFeedbackController` | 2 | 图片压缩/存储、WX_Feedback(+Image) |
| `Admin_FeedbackController` | 3 | 同上 |

### 批次 4：查询大本营（工作量最大）

| 控制器 | 端点数 | 依赖 |
|---|---|---|
| `MobileQueryController` | 24 | 消费/订餐/考勤/门禁/访客/菜谱全部报表，含大量原生 SQL |

建议按子域拆：account 相关 → canteen（菜谱/订餐/订单/取消/份数）→ attendance → access/visitor 查询。

### 批次 5：管理端与基础资料

| 控制器 | 端点数 |
|---|---|
| `Admin_OrderController` | 2 |
| `DC_OrderDataController` / `ST_*Controller` | 0（空骨架，可选） |

## 三、移植规则（EF6 → EF Core）

| Net（EF6 / Web API 2） | NetCore（EF Core / MVC） |
|---|---|
| `[RoutePrefix("api/m/x")]` + `[HttpGet, Route("y")]` | `[Route("api/m/x")]` + `[HttpGet("y")]` |
| `ApiController` / `IHttpActionResult` | `ControllerBase` / `IActionResult` |
| `(User as ClaimsPrincipal).Claims.First(c => c.Type == "PersonId")` | `User.FindFirst("PersonId")?.Value` |
| `db.XF_Consume`（单数 DbSet） | `db.XF_Consumes`（**注意：ST_Person → `ST_People`**） |
| `db.Database.SqlQuery<T>(sql, SqlParameter...)` | `db.Database.SqlQueryRaw<T>(sql, ...).ToListAsync()` |
| `db.Database.ExecuteSqlCommand(sql, args)` | `db.Database.ExecuteSqlRawAsync(sql, args)` |
| `new PsnMgrEntities()` 每方法自建 | 构造函数注入 `AppDbContext` |
| `ConfigurationManager.AppSettings["x"]` | `AppConfig.X`（启动时 Init） |
| `[AllowAnonymous]`（Web API 2） | `[AllowAnonymous]`（需显式，全局 `MapControllers().RequireAuthorization()`） |
| `SqlParameter`（System.Data.SqlClient） | `Microsoft.Data.SqlClient.SqlParameter` |
| `float`/`money` → `double`/`decimal` | 注意可空性，EF Core 脚手架已按库结构生成 |

## 四、必须注意的坑

1. **`Program.cs` 是 GBK 编码**，脚本直接按 UTF-8 读写会报 `UnicodeDecodeError`；改动请用字节级替换或指定 `gb18030`。
2. **全局鉴权**：`app.MapControllers().RequireAuthorization()` 已存在，新增 controller 默认需登录；匿名接口必须显式 `[AllowAnonymous]`
   （与 Net 的全局 `AuthorizeAttribute` + `AllowAnonymous` 行为一致）。
3. **两套 token**：管理员（ST_UserInfo）与移动端（ST_Person，claim `PersonId`）不能混用。
4. **请求体模型重名**：如 `ChangePwdReq` 等类名在 Net 端是全局命名空间，移植后注意与已有类型冲突（必要时加 `Mobile` 前缀）。
5. **日期/时间**：Net 端大量 `ToString("yyyy-MM-dd HH:mm")`，保持一致，别改成 ISO。
6. **返回字段大小写**：NetCore 默认 camelCase 序列化，与 Net 的默认（也是 camelCase for Web API JSON）一致；但匿名对象里显式命名以源码为准。
7. **原生 SQL 的列名**：`SqlQueryRaw<T>` 要求 T 的属性名与 SELECT 别名**严格一致**（大小写不敏感但不做驼峰转换）。
8. **事务 + 锁**：访客卡分配用 `WITH (UPDLOCK, ROWLOCK)`，EF Core 里同样用 `SqlQueryRaw` + 显式事务。

## 五、验收方式

1. 每个控制器移植后 `dotnet build` 必须 0 error。
2. 用 Apifox/curl 对**同一份请求**分别打 Net 与 NetCore，逐字段比对 JSON（可用 `Doc/ApifoxTest` 里已有用例）。
3. 重点回归：登录 → 消费/充值记录 → 菜谱 → 订餐（两种扣款模式）→ 考勤申请 → 访客全链路（预约/审批/凭证/签到/签离/作废）。

---

## 进度更新（2026-11-14）

| 批次 | 内容 | 状态 |
|---|---|---|
| ① | MobileAuth(7) + MobileAccount(6) + MobileMessage(3) | ✅ 完成 |
| ② | Recharge(4) + PayOrderService/DbApiLogger/WeChatPayService | ✅ 完成 |
| ③ | MobileVisitor(12) + VisitorCardPool | ✅ 完成 |
| ④ | MobileAttendanceApply(6) + KqApplyHelper + Admin_KQ_Apply(2) | ✅ 完成 |
| ⑤ | MobileFeedback(2) + Admin_Feedback(3) | ✅ 完成 |
| ⑥ | Admin_Order(2) | ✅ 完成 |
| ⑦ | MobileAccess(9) + MobileCanteen(9) + MobileFace(6) = MobileQuery 的 24 个端点 | ✅ 完成 |

NetCore 累计 **77** 个端点（与 Net 端对齐），`dotnet build` 0 error。

**补充坑（本轮踩到）**
1. `WeChatPayService` 里 SKIT 的 `ExecuteXxxAsync` 是扩展方法，必须 `using SKIT.FlurlHttpClient.Wechat.TenpayV2; / ...TenpayV3;`，否则报 CS1061。
2. EF Core 标量查询 `SqlQueryRaw<int>` 必须写成 `SELECT COUNT(1) AS Value`。
3. 财务类写库（充值入账、撤销订餐退款、考勤回写）统一用 `SqlConnection`+`SqlTransaction`，不混 EF 事务。
4. `Program.cs` 为 GBK 编码，任何脚本修改需字节级替换。

**最后一批（face/scancode/service）的已知差异**
- `face/upload` **未做人脸/姿态检测**：Net 端依赖 `PsnMgr.FaceDetect.PicoDetector`（含模型数据），NetCore 侧尚未移植。
  目前保留体积（<80K 压缩）、分辨率≥200×200、亮度 0.15~0.92、内容方差≥0.02 四项审核，通过后落 `ST_PersonPhoto`。
  上线前需二选一：移植 PicoDetector，或改用外部人脸服务（腾讯云/阿里云人脸比对）。
- `scancode/payment|pass` 支持 `YKT_PaymentCode=1` 走一卡通 CRC 码（`Services/JOCat.cs`），否则用 `employee;工号;时间戳`。
- `System.Drawing.Common` 9.0.0 已引入（仅 Windows 可用，与现有部署一致）。

---

## 收尾（2026-11-14 晚）

**已补齐**
- `FaceDetect/PicoDetector.cs` + `PicoCascade.cs`：从 Net 端整目录移植（命名空间改 `PsnMgrService.FaceDetect`），
  `face/upload` 现已恢复「是否检测到人脸 + 人脸朝向是否正立」判定，与 H5/小程序同一套级联与阈值。
- `Services/WeChatTemplateService.cs`：服务号模板消息 + 小程序订阅消息，已接入 `MobileMessageService.Push`
  （`Task.Run` 异步、失败静默，与 Net 一致）。模板ID 配在 `appsettings.json` 的 `WeChatTemplate:{WxTpl_*|WxSub_*}`。
- `AppConfig.Configuration` 暴露 IConfiguration，供按 key 动态读模板ID。

**目录结构对齐 Net**
```
Controllers/
  Admin_FeedbackController.cs  Admin_KQ_ApplyController.cs  Admin_OrderController.cs
  AuthController.cs            ST_DepartmentController.cs
  MicroApi/
    MobileAuthController.cs  MobileAccountController.cs  MobileMessageController.cs
    MobileVisitorController.cs  MobileAttendanceApplyController.cs  MobileFeedbackController.cs
    MobileAccessController.cs  MobileCanteenController.cs  MobileFaceController.cs
    RechargeController.cs
```
> Net 端把 `KqApplyHelper`/`VisitorCardPool` 也放在 `Controllers/MicroApi`；NetCore 侧保留在 `Services/`
> （跨控制器共用，且 namespace 引用较多，移动会牵动 5 处 using），目录只差这两个文件。

**路由对齐结果**（脚本逐条比对）：Net 72 / NetCore 77
- 仅 Net 有：无（`canteen/photo/{id}` 与 `api/auth/login` 已按 Net 写法对齐）。
- 仅 NetCore 有：`api/ST_Department` 的 5 个 CRUD —— 属**超集**，管理端在用，不影响兼容。

**仍缺（不影响接口功能）**
- `Services/ApiLogHandler.cs`（Net 的全局 `DelegatingHandler`，把每个请求/响应写 `WX_ApiLog`）：NetCore 未移植。
  支付/短信/微信推送的日志仍由 `DbApiLogger` 显式写入，**只是少了接口级流水**。需要的话加一个中间件即可。
- `face/upload` 审核不通过时 Net 会把图片存 `~/Logs`（排查用），NetCore 未落盘。

---

## 联调结论与 SQL Server 2008 兼容铁律（2026-11-14）

### 环境事实（决定写法）
```
SELECT compatibility_level, SERVERPROPERTY('ProductVersion') FROM sys.databases WHERE name='STCard42Sample'
→ compat = 90，version = 10.50.1600.1（SQL Server 2008 R2）
```
EF Core 9 官方要求 SQL Server 2016+，在 2008 R2 上生成的 SQL 会直接语法报错。
**compat 升到 100 也救不了**（2008 R2 上限 100，没有 `OFFSET/FETCH`，那是 110+ 才有）。

### 四条铁律（新写查询必须遵守）

| # | 禁止 | 改用 | 症状 |
|---|---|---|---|
| 1 | `.Skip(n).Take(m)`（EF 分页） | Dapper + `ROW_NUMBER() OVER(...) AS RowNum` + `WHERE RowNum BETWEEN` | `OFFSET 附近有语法错误` |
| 2 | `list.Contains(x)` 形式的 EF 查询 | Dapper `WHERE col IN @list`（Dapper 自动展开） | `关键字 WITH 附近有语法错误`（EF 翻成 OPENJSON/CTE） |
| 3 | 在 EF 之上组合 `Database.SqlQueryRaw<T>` + 额外 LINQ | 直接 `Db.QueryAsync<T>(sql)` | 同上（EF 会把 raw 包进 CTE） |
| 4 | `SqlQueryRaw<T>` 的载体属性与 SELECT 列不一致 | 载体属性 = SELECT 列，**一列不多一列不少** | 缺列异常 / 字段恒为默认值 |

**补充**：`RowNum` 这类由常量填充的列，字面量要给足类型 —— `0 AS RowNum` 映射到 `long` 会抛
`Unable to cast object of type 'System.Int32' to type 'System.Int64'`，必须写 `CAST(0 AS bigint) AS RowNum`。

### Dapper 统一入口
`Services/Db.cs`：`Db.QueryAsync<T> / Query<T> / QueryFirstAsync<T> / ExecuteAsync`，内部读 `AppConfig.ConnectionString`。
新增查询优先用它，不要再用 EF 的 `SqlQueryRaw`。

### 联调结果（本机 5187 端口 + STCard42Sample 测试库 + 自签移动端 JWT）

**读接口全部 200（34 个）**
`service/info`、`auth/profile`、`account/rechargerecords|consumptions`、
`canteen/areas|repasts|menu|today|ordermode|myorders`、`leave/kinds|list`、
`visitor/list|persons|cards|photo|qrcode`、`message/list`、`feedback/list`、
`access/doors|rights|normal|abnorm`、`face/status`、`scancode/payment|pass`、
`attendance/daily`、`attapply/list|resign-count`、
`Admin_Order`、`Admin_KQ_Apply`、`Admin_Feedback`

**写链路实测（数据已清理）**

| 链路 | 结果 |
|---|---|
| 订餐下单→退订 | 下单价 24（12×1 + 6×2），余额 100→76，写 `DC_OrderData` + `DC_OrderDataSub` + `XF_Consume(Consume_Mode=20)`；退订返回 `refunded=true / refundAmount=24`，余额回到 100，写 `Consume_Mode=21` 负数流水，订单主/子表物理删除 |
| 访客预约→审批（分配卡）→签到→签离 | 提交 `Start/End=14:00~16:00`、`visitors_number=2`；审批分配卡 `13941371` 并把 `ST_Card.Card_ValidDate` 延到 16:00；签到写 `check_intime`；签离把 `Card_ValidDate` 置为当前时刻（卡立即失效） |
| 访客（不分配卡）凭证 | `noCard=true` → `visitToken=VIS1.{applyNo}.{exp}.{sig}`；匿名 `verify` 返回 `valid:true`、手机脱敏 `138****3333`、`state=待签到` |
| 补签申请→审批 | 写 `KQ_SupplyTime`（`Supply_Class` 1 上班 / 2 下班，`Target_Table/Target_Ids` 回填），审批后 `EXEC KQ_pd_CalcDaily` 成功（`calcTip` 为空） |

**性能注意**：`KQ_pd_CalcDaily` 在这个远程库上**单日要 2–3 分钟**，默认 30s 命令超时必然中断。
`Admin_KQ_Apply.audit` 已 `SetCommandTimeout(600)`；`attendance/daily` 的补算同理。生产环境同机房会快很多，但别再退回默认值。

### 部署前仍需人工确认
1. `appsettings.json` 里的 `WeChat`（MchId / ApiV2Key / ApiV3Key / SerialNo / NotifyUrl / PrivateKeyPath）、`AliyunSms`、`JwtSettings.SecretKey`、`VisitorQrSecret`、`WeChatTemplate:WxTpl_*|WxSub_*` 已填真实值。
2. 微信商户平台通知地址指向 NetCore 站点 `/api/m/recharge/notify/v3`。
3. `face/upload`、`recharge/create` 需要真实设备/商户环境，未在本轮联调覆盖。
