select * from st_sysparam where param_code='db_update_version'
go


if exists(select * from syscolumns where id=object_id('ST_Department') and name='Dept_Name')  
	alter table ST_Department alter column Dept_Name varchar(100)
if not exists(select * from syscolumns where id=object_id('ST_Department') and name='Dept_Sort')  
	alter table ST_Department add Dept_Sort int null	
if not exists(select * from syscolumns where id=object_id('ST_Department') and name='Dept_Telphone')
	alter table ST_Department add Dept_Telphone varchar(30) null	
if not exists(select * from syscolumns where id=object_id('ST_Department') and name='Create_Time')  
	alter table ST_Department add Create_Time datetime null	
if NOT exists(select * from syscolumns where id=object_id('ST_SupplyFund') and name='Out_Trade_No')
	ALTER TABLE ST_SupplyFund ADD Out_Trade_No varchar(64) NULL
if NOT exists(select * from syscolumns where id=object_id('ST_SupplyFundHistory') and name='Out_Trade_No')
	ALTER TABLE ST_SupplyFundHistory ADD Out_Trade_No varchar(64) NULL
if NOT exists(select * from syscolumns where id=object_id('XF_WareInWeek') and name='Area_No')--食堂编号
	ALTER TABLE XF_WareInWeek ADD Area_No varchar(12) NULL
if NOT exists(select * from syscolumns where id=object_id('XF_WareInWeek') and name='Order_Copies')--可预订份数
	ALTER TABLE XF_WareInWeek ADD Order_Copies int NULL
go

IF OBJECT_ID('dbo.SK_ScanCode') IS NULL
		CREATE TABLE dbo.SK_ScanCode (
			   ID_Key               bigint IDENTITY,       
			   Moc_No               varchar(8),--扫码终端	   
			   Get_Time             datetime NULL,--扫码/充值时间
			   Person_ID            int NULL,--扫码人
			   Person_No            varchar(32) NULL,--扫码人	   
			   QR_Fund              money null,--(预消费金额) 账户余额（4）+
			   QR_Fund_Free         money null,--免费金额（4）
			   State                int not null default(0), --状态(0：未处理,1:已给消费终端,2:未处理钱退回主帐户)
			   moc_read_time        datetime null,--终端读取时间
			   Remark               varchar(20) NULL,
			   soruce_id_key        bigint,--源ID
			   Consume_TimeLen      float null,
			   open_id              varchar(100),
			   
			   pay_type             int not null default(0),--支付方式（0：帐户余额，1微信支付）
			   pay_state            int null default(0),--支付状态（0：未支付成功,1支付成功）

			   Consume_Mode         int null,--扣费模式(1)/用水模式/计流量/计时计次（不用，从终端机取）
			   Unit_Price           money null,--扣款单位金额4（不用，从终端机取）
			   Unit                 int null,--扣款单位4 （不用，从终端机取）		   
			   Unit_Number          int null,--计费间隔 （不用，从终端机取）
			   QR_Code              varchar(256),--原始二维吗（用不到）
			   PRIMARY KEY (ID_Key ASC)
		)
go

/* ---------- 1. 访客临时卡池 ---------- */
IF OBJECT_ID(N'dbo.WX_VisitorCard', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WX_VisitorCard
    (
        Card_No        varchar(12)  NOT NULL,          -- 临时卡号（写入 MJ_Person.Acs_Card）
        Mj_Person_ID   int          NOT NULL,          -- 该卡对应的门禁人员槽位（MJ_Person.Person_ID）
        State          tinyint      NOT NULL CONSTRAINT DF_WX_VisitorCard_State DEFAULT(0),
                                                       -- 0=空闲 1=占用 2=停用
        Apply_No       varchar(50)  NULL,              -- 当前占用的访客申请单
        Bind_Time      datetime     NULL,
        Release_Time   datetime     NULL,
        Remark         nvarchar(100) NULL,
        CONSTRAINT PK_WX_VisitorCard PRIMARY KEY CLUSTERED (Card_No)
    );

    CREATE INDEX IX_WX_VisitorCard_State ON dbo.WX_VisitorCard (State, Card_No);
END
GO

/* ---------- 2. 访客授权门点（ST_SysParam） ----------
   Param_Value 格式："控制器编号-门编号,控制器编号-门编号"
   例：01-1,01-2 表示控制器 01 的 1 号门、2 号门。
   未配置时：审批只分配卡号、不写门禁授权（卡刷不开门），并在返回里提示。            */
IF NOT EXISTS (SELECT 1 FROM dbo.ST_SysParam WHERE Param_Code = 'VIS.DoorRights')
BEGIN
    INSERT INTO dbo.ST_SysParam (Param_Code, Param_Name, Param_Value)
    VALUES ('VIS.DoorRights', N'访客授权门点（控制器-门，逗号分隔）', N'');
END
GO

/* ---------- 3. 现场登记卡池（示例，按实际卡号/人员槽位修改后执行） ----------
   Mj_Person_ID 必须是 MJ_Person 里专供访客卡使用的固定人员，不能复用员工。
   建议先在 MJ_Person 建 N 个「访客卡槽」人员（User_No 如 VISITOR01..N），再登记到这里。

INSERT INTO dbo.WX_VisitorCard (Card_No, Mj_Person_ID, State, Remark)
VALUES ('10000001', 20001, 0, N'访客卡1号'),
       ('10000002', 20002, 0, N'访客卡2号'),
       ('10000003', 20003, 0, N'访客卡3号');
*/

/*===========================================================
  微信移动端对接扩展脚本（H5服务号 + 微信小程序）
  执行顺序：1、glm_KQ_CalcDaily.sql（或KiMi版）→ 2、update.sql → 本脚本
===========================================================*/
IF OBJECT_ID('WX_RechargeLog') IS NULL
CREATE TABLE WX_RechargeLog(
  ID_KEY bigint identity(1,1) primary key,
  Person_ID int NULL,
  Out_Trade_No varchar(64) NOT NULL,
  Transaction_Id varchar(64) NULL,
  Total_Fee decimal(18,2) NULL,
  Status varchar(20) NULL,                 -- CREATED/SUCCESS/FAILED/NO_CARD/NO_PERSON/CLOSED
  Err_Code varchar(32) NULL, Err_Msg varchar(500) NULL,
  Trade_Type varchar(10) NULL, Channel varchar(10) NULL,   -- JSAPI/MINI/NATIVE ; V3/V2
  Raw_Send nvarchar(max) NULL, Raw_Recv nvarchar(max) NULL,
  Create_Time datetime default getdate(), Update_Time datetime NULL)
GO
IF OBJECT_ID('WX_ApiLog') IS NULL
CREATE TABLE WX_ApiLog(
  ID_KEY bigint identity(1,1) primary key,
  Person_ID int NULL, Api_Url varchar(200), Http_Method varchar(10),
  Request_Body nvarchar(max) NULL, Response_Body nvarchar(max) NULL,
  IP varchar(50) NULL, Elapsed int NULL, Success bit NULL,
  Create_Time datetime default getdate())
GO
IF OBJECT_ID('WX_SmsCode') IS NULL
CREATE TABLE WX_SmsCode(
  ID_KEY bigint identity(1,1) primary key,
  Mobile varchar(20), Code varchar(8), Scene varchar(20),
  Expire_Time datetime, Used bit default 0, Create_Time datetime default getdate())
GO

/* 考勤申诉表（补签走现有 KQ_FillAttend）**/
IF OBJECT_ID('WX_KQ_Appeal') IS NULL
CREATE TABLE WX_KQ_Appeal(
  ID_KEY bigint identity(1,1) primary key,
  Person_ID int NOT NULL,
  Appeal_Date varchar(10),
  Appeal_Type varchar(40),
  Reason varchar(500),
  Status varchar(10) default 'pending',   -- pending/approved/rejected（管理平台审批）
  User_No varchar(20),
  Create_Time datetime default getdate(),
  Update_Time datetime default getdate())
GO


IF OBJECT_ID(N'dbo.WX_KQ_Apply', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WX_KQ_Apply
    (
        ID_KEY        bigint         IDENTITY(1,1) NOT NULL,
        Apply_No      varchar(30)    NOT NULL,          -- 申请号：KQ + yyyyMMddHHmmss + 3位随机
        Person_ID     int            NOT NULL,
        Person_No     varchar(30)    NULL,
        Person_Name   nvarchar(50)   NULL,
        Apply_Type    varchar(10)    NOT NULL,          -- resign / leave / appeal

        -- 补签
        Supply_Date   varchar(10)    NULL,              -- yyyy-MM-dd
        Supply_Time   varchar(5)     NULL,              -- HH:mm
        Supply_Class  smallint       NULL,              -- 1=上班 2=下班

        -- 请假（允许跨天，首尾保留时间）
        Kind_No       varchar(4)     NULL,
        Kind_Name     nvarchar(20)   NULL,
        Leave_Bgn     varchar(20)    NULL,              -- yyyy-MM-dd HH:mm
        Leave_End     varchar(20)    NULL,              -- yyyy-MM-dd HH:mm
        Leave_Hour    float          NULL,              -- 自然小时数（合计）
        Leave_Days    int            NULL,              -- 覆盖天数

        -- 申诉
        Appeal_Date   varchar(10)    NULL,
        Appeal_Type   nvarchar(20)   NULL,              -- 迟到误判 / 早退误判 / 缺卡异常
        Appeal_Time   varchar(5)     NULL,              -- 应为的打卡时刻（回写补签用）

        Reason        nvarchar(200)  NULL,
        Status        tinyint        NOT NULL CONSTRAINT DF_WX_KQ_Apply_Status DEFAULT(0),
        Audit_User    nvarchar(50)   NULL,
        Audit_Time    datetime       NULL,
        Audit_Remark  nvarchar(200)  NULL,
        Target_Table  varchar(20)    NULL,
        Target_Ids    nvarchar(400)  NULL,
        Source        varchar(10)    NULL,              -- h5 / mp / legacy
        Create_Time   datetime       NOT NULL CONSTRAINT DF_WX_KQ_Apply_Create_Time DEFAULT(GETDATE()),
        CONSTRAINT PK_WX_KQ_Apply PRIMARY KEY CLUSTERED (ID_KEY)
    );

    CREATE UNIQUE INDEX UX_WX_KQ_Apply_No ON dbo.WX_KQ_Apply (Apply_No);
    CREATE INDEX IX_WX_KQ_Apply_Person ON dbo.WX_KQ_Apply (Person_ID, Create_Time DESC);
    CREATE INDEX IX_WX_KQ_Apply_Audit ON dbo.WX_KQ_Apply (Status, Apply_Type, Create_Time DESC);
END
GO
/* 消息中心（站内信，业务事件自动推送）*/
IF OBJECT_ID('WX_Message') IS NULL
CREATE TABLE WX_Message(
  ID_KEY bigint identity(1,1) primary key,
  Person_ID int NOT NULL,
  Msg_Type varchar(20),          -- recharge/consumption/order/visitor/attendance/face/system
  Title nvarchar(200),
  Content nvarchar(1000),
  Is_Read bit default 0,
  Create_Time datetime default getdate())
GO

/* ---------- 1. 工单主表 ---------- */
IF OBJECT_ID(N'dbo.WX_Feedback', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WX_Feedback
    (
        ID_KEY        bigint        IDENTITY(1,1) NOT NULL,
        Ticket_No     varchar(30)   NOT NULL,          -- 工单号：GD + yyyyMMddHHmmss + 3位随机
        Person_ID     int           NOT NULL,          -- 提交人（取自登录 token）
        Person_No     varchar(30)   NULL,              -- 工号快照，便于后台按工号检索
        Person_Name   nvarchar(50)  NULL,              -- 姓名快照
        Card_No       varchar(30)   NULL,              -- 卡号快照
        Feed_Type     nvarchar(20)  NOT NULL,          -- 反馈类型：消费/门禁/订餐/访客/考勤/其他问题
        Content       nvarchar(1000) NOT NULL,         -- 问题描述（最短 10 字，服务端校验）
        Contact       nvarchar(100) NOT NULL,          -- 联系方式（手机号/邮箱）
        Image_Count   int           NOT NULL CONSTRAINT DF_WX_Feedback_Image_Count DEFAULT(0),
        Status        tinyint       NOT NULL CONSTRAINT DF_WX_Feedback_Status DEFAULT(0),
                                                       -- 0=待处理 1=处理中 2=已回复 3=已关闭
        Reply_Content nvarchar(1000) NULL,             -- 处理回复
        Reply_User    nvarchar(50)  NULL,              -- 处理人
        Reply_Time    datetime      NULL,
        Source        varchar(10)   NULL,              -- 来源：h5 / mp
        Create_Time   datetime      NOT NULL CONSTRAINT DF_WX_Feedback_Create_Time DEFAULT(GETDATE()),
        CONSTRAINT PK_WX_Feedback PRIMARY KEY CLUSTERED (ID_KEY)
    );

    CREATE UNIQUE INDEX UX_WX_Feedback_TicketNo ON dbo.WX_Feedback (Ticket_No);
    CREATE INDEX IX_WX_Feedback_Person ON dbo.WX_Feedback (Person_ID, Create_Time DESC);
    CREATE INDEX IX_WX_Feedback_Status ON dbo.WX_Feedback (Status, Create_Time DESC);
END
GO

/* ---------- 2. 图片子表 ---------- */
IF OBJECT_ID(N'dbo.WX_FeedbackImage', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WX_FeedbackImage
    (
        ID_KEY       bigint         IDENTITY(1,1) NOT NULL,
        Ticket_No    varchar(30)    NOT NULL,
        Seq          int            NOT NULL,           -- 第几张，从 1 开始
        Content_Type varchar(30)    NULL,               -- image/jpeg、image/png
        Photo        varbinary(max) NOT NULL,
        Create_Time  datetime       NOT NULL CONSTRAINT DF_WX_FeedbackImage_Create_Time DEFAULT(GETDATE()),
        CONSTRAINT PK_WX_FeedbackImage PRIMARY KEY CLUSTERED (ID_KEY)
    );

    CREATE INDEX IX_WX_FeedbackImage_Ticket ON dbo.WX_FeedbackImage (Ticket_No, Seq);
END
GO
/* ST_SysParam 结构：Param_ID int IDENTITY 主键 / Param_Code varchar(20) / Param_Name varchar(100) / Param_Value varchar(100)，均 NOT NULL */

IF NOT EXISTS (SELECT 1 FROM dbo.ST_SysParam WHERE Param_Code = 'SYS.ServicePhone')
    INSERT INTO dbo.ST_SysParam (Param_Code, Param_Name, Param_Value)
    VALUES ('SYS.ServicePhone', N'客服电话', N'400-888-8888');

IF NOT EXISTS (SELECT 1 FROM dbo.ST_SysParam WHERE Param_Code = 'SYS.ServiceWindow')
    INSERT INTO dbo.ST_SysParam (Param_Code, Param_Name, Param_Value)
    VALUES ('SYS.ServiceWindow', N'服务窗口', N'行政楼一楼服务大厅');

IF NOT EXISTS (SELECT 1 FROM dbo.ST_SysParam WHERE Param_Code = 'SYS.ServiceTime')
    INSERT INTO dbo.ST_SysParam (Param_Code, Param_Name, Param_Value)
    VALUES ('SYS.ServiceTime', N'服务时间', N'工作日 9:00-17:00');
GO

/*====================== 0. 辅助标量函数 ======================*/
IF OBJECT_ID('dbo.fnKQ_To24Hour') IS NOT NULL DROP FUNCTION dbo.fnKQ_To24Hour
GO
CREATE FUNCTION dbo.fnKQ_To24Hour(@TimeStr VARCHAR(5), @aDate DATETIME)
RETURNS DATETIME AS            -- fnTo24Hour: 处理跨天时间(24:30=次日00:30)
BEGIN
    DECLARE @r DATETIME, @s VARCHAR(5), @h INT
    SET @s = LTRIM(RTRIM(ISNULL(@TimeStr,'')))
    IF @s='' OR LEN(@s)<4 RETURN NULL
    IF SUBSTRING(@s,1,2) >= '24'
    BEGIN
        SET @h = CAST(SUBSTRING(@s,1,2) AS INT) - 24
        SET @s = RIGHT('0'+CAST(@h AS VARCHAR(2)),2) + SUBSTRING(@s,3,3)
        SET @r = CAST(CONVERT(VARCHAR(10), DATEADD(day,1,@aDate),120) + ' ' + @s AS DATETIME)
    END
    ELSE
        SET @r = CAST(CONVERT(VARCHAR(10), @aDate,120) + ' ' + @s AS DATETIME)
    RETURN @r
END
GO
IF OBJECT_ID('dbo.fnKQ_MinutesBetween') IS NOT NULL DROP FUNCTION dbo.fnKQ_MinutesBetween
GO
CREATE FUNCTION dbo.fnKQ_MinutesBetween(@Now DATETIME, @Then DATETIME)
RETURNS INT AS                 -- fnMinutesBetween: (Now-Then)分钟数,四舍五入,带符号
BEGIN
    DECLARE @s INT, @m INT
    SET @s = DATEDIFF(second, @Then, @Now)
    SET @m = CAST(ROUND(ABS(CAST(@s AS DECIMAL(18,4)))/60.0, 0) AS INT)
    IF @s < 0 SET @m = -@m
    RETURN @m
END
GO
IF OBJECT_ID('dbo.fnKQ_FormatData') IS NOT NULL DROP FUNCTION dbo.fnKQ_FormatData
GO
CREATE FUNCTION dbo.fnKQ_FormatData(@Value DECIMAL(18,4), @MinToHour INT,
                                    @OneHour INT, @HalfHour INT, @Decimals INT)
RETURNS DECIMAL(18,2) AS       -- fnFormatData: 分钟转小时
BEGIN
    DECLARE @r DECIMAL(18,2), @li_Int INT, @li_Dec DECIMAL(18,4)
    IF @MinToHour = 0          -- 参数 mintohour=0: 按1或0.5小时取整
    BEGIN
        SET @li_Int = FLOOR(@Value/60.0)
        SET @li_Dec = @Value - @li_Int*60
        IF @li_Dec >= @OneHour SET @r = @li_Int + 1
        ELSE IF @li_Dec >= @HalfHour SET @r = @li_Int + 0.5
        ELSE SET @r = @li_Int
    END
    ELSE
    BEGIN
        SET @Value = @Value/60.0
        IF @Decimals = 0 SET @r = ROUND(@Value,0)
        ELSE IF @Decimals = 1 SET @r = ROUND(@Value,1)
        ELSE SET @r = ROUND(@Value,2)
    END
    RETURN @r
END
GO
IF OBJECT_ID('dbo.fnKQ_FactWorkHour') IS NOT NULL DROP FUNCTION dbo.fnKQ_FactWorkHour
GO
CREATE FUNCTION dbo.fnKQ_FactWorkHour(@Must DECIMAL(18,4), @Fact DECIMAL(18,4), @Deduct DECIMAL(18,4))
RETURNS DECIMAL(18,4) AS       -- fnGetFactWorkHour: 一天一个段次的实出勤处理
BEGIN
    DECLARE @r DECIMAL(18,4)
    IF @Fact > @Must*0.5
    BEGIN
        IF @Fact < @Must*0.5 + @Deduct + 1 SET @r = @Must*0.5
        ELSE SET @r = @Fact - @Deduct
    END
    ELSE SET @r = @Fact
    RETURN @r
END
GO
IF OBJECT_ID('dbo.fnKQ_ChangeTime') IS NOT NULL DROP FUNCTION dbo.fnKQ_ChangeTime
GO
CREATE FUNCTION dbo.fnKQ_ChangeTime(@BrushDate VARCHAR(10), @BrushTime VARCHAR(8), @aDate VARCHAR(10))
RETURNS VARCHAR(8) AS          -- fn_ChangeTime: 跨天刷卡时间小时+24*n
BEGIN
    DECLARE @d INT, @h INT
    SET @d = DATEDIFF(day, CAST(@aDate AS DATETIME), CAST(@BrushDate AS DATETIME))
    SET @h = CAST(SUBSTRING(@BrushTime,1,2) AS INT) + 24*@d
    RETURN RIGHT('0'+CAST(@h AS VARCHAR(3)),2) + SUBSTRING(@BrushTime,3,LEN(@BrushTime)-2)
END
GO

/*====================== 1. 内部过程: 装载班次段次 (fnSetClassInfo) ======================*/
IF OBJECT_ID('dbo.KQ_pd_SetClassInfo') IS NOT NULL DROP PROC dbo.KQ_pd_SetClassInfo
GO
CREATE PROC dbo.KQ_pd_SetClassInfo @ClassNo VARCHAR(20)
AS                             -- 返回1成功 / 0没有找到班次; 结果写入 #ClassSect、#Ctx
BEGIN
    SET NOCOUNT ON
    DELETE FROM #ClassSect
    INSERT INTO #ClassSect (SectNo,TimeIn,TimeOut,InCard,OutCard,Deduct,Overtime,IncBgnIn,IncEndIn,IncBgnOut,IncEndOut)
    SELECT 1,Sect1_InTime,Sect1_OutTime,Sect1_InCard,Sect1_OutCard,Sect1_Deduct,Sect1_Overtime,
           -ISNULL(Sect1_Ahead,0), c.Late3E, -c.Early3E, ISNULL(Sect1_After,0)
      FROM KQ_Class k CROSS JOIN #Ctx c WHERE k.Class_No=@ClassNo AND c.Id=1
    UNION ALL SELECT 2,Sect2_InTime,Sect2_OutTime,Sect2_InCard,Sect2_OutCard,Sect2_Deduct,Sect2_Overtime,
           -ISNULL(Sect2_Ahead,0), c.Late3E, -c.Early3E, ISNULL(Sect2_After,0)
      FROM KQ_Class k CROSS JOIN #Ctx c WHERE k.Class_No=@ClassNo AND c.Id=1
    UNION ALL SELECT 3,Sect3_InTime,Sect3_OutTime,Sect3_InCard,Sect3_OutCard,Sect3_Deduct,Sect3_Overtime,
           -ISNULL(Sect3_Ahead,0), c.Late3E, -c.Early3E, ISNULL(Sect3_After,0)
      FROM KQ_Class k CROSS JOIN #Ctx c WHERE k.Class_No=@ClassNo AND c.Id=1
    UNION ALL SELECT 4,Sect4_InTime,Sect4_OutTime,Sect4_InCard,Sect4_OutCard,Sect4_Deduct,Sect4_Overtime,
           -ISNULL(Sect4_Ahead,0), c.Late3E, -c.Early3E, ISNULL(Sect4_After,0)
      FROM KQ_Class k CROSS JOIN #Ctx c WHERE k.Class_No=@ClassNo AND c.Id=1
    UNION ALL SELECT 5,Sect5_InTime,Sect5_OutTime,Sect5_InCard,Sect5_OutCard,Sect5_Deduct,Sect5_Overtime,
           -ISNULL(Sect5_Ahead,0), c.Late3E, -c.Early3E, ISNULL(Sect5_After,0)
      FROM KQ_Class k CROSS JOIN #Ctx c WHERE k.Class_No=@ClassNo AND c.Id=1

    IF NOT EXISTS(SELECT 1 FROM #ClassSect) RETURN 0          -- 没有找到班次

    UPDATE #Ctx SET ClassNo=@ClassNo,
           ClassName=(SELECT Class_Name FROM KQ_Class WHERE Class_No=@ClassNo),
           ClassData=ISNULL(STUFF((SELECT ','+LTRIM(RTRIM(TimeIn))+'-'+LTRIM(RTRIM(TimeOut))
                                     FROM #ClassSect WHERE TimeIn<>'' AND TimeOut<>'' ORDER BY SectNo
                                     FOR XML PATH('')),1,1,''),'')
     WHERE Id=1
    RETURN 1
END
GO

/*====================== 2. 内部过程: 初始化日报变量 (fnInitDailyInfo) ======================*/
IF OBJECT_ID('dbo.KQ_pd_InitDailyInfo') IS NOT NULL DROP PROC dbo.KQ_pd_InitDailyInfo
GO
CREATE PROC dbo.KQ_pd_InitDailyInfo AS
BEGIN
    UPDATE #Ctx SET MustWork=0, FactWork=0, OvertimeHour=0, OvertimeClass='',
           LateMinute=0, LateData='', EarlyMinute=0, EarlyData='',
           Reason='', AbsentWork=0, LeaveHour=0, LeaveData='', LeaveName='', OverAbsent=0,
           Sect1Must=0, Sect1Fact=0, Sect1LateMinute=0, Sect1EarlyMinute=0,
           Sect1LateData='', Sect1EarlyData=''
     WHERE Id=1
END
GO

/*====================== 3. 内部过程: 班次匹配刷卡 + 自动补卡 ======================
   对应 fnMatchBrushCard / fnMatchBrushInCard / fnMathcBrushOutCard / fnAutoFillCard
=======================================================================================*/
IF OBJECT_ID('dbo.KQ_pd_MatchClassSect') IS NOT NULL DROP PROC dbo.KQ_pd_MatchClassSect
GO
CREATE PROC dbo.KQ_pd_MatchClassSect @CurDateDT DATETIME
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @Mode INT, @OvertimeBig INT, @Late3E INT, @OutAddMinute INT,
            @SectNo INT, @RangeSect INT, @WriteSect INT,
            @RInBgn DATETIME, @RInEnd DATETIME, @ROutBgn DATETIME, @ROutEnd DATETIME,
            @InTime DATETIME, @OutTime DATETIME, @InCard INT, @OutCard INT, @Overtime BIT,
            @RInCard INT, @RInFact DATETIME,
            @i INT, @MaxIdx INT, @BrushDT DATETIME, @DataFlag INT, @li_Minute INT,
            @ld_OutTime DATETIME, @CardState INT, @j INT,
            @sOutTime DATETIME, @sOutFact DATETIME, @sInTime DATETIME, @sInFact DATETIME,
            @sInCard INT, @sOutCard INT

    SELECT @Mode=Worktime_Mode, @OvertimeBig=OvertimeBig, @Late3E=Late3E, @OutAddMinute=OutAddMinute
      FROM #Ctx WHERE Id=1
    SELECT @MaxIdx=ISNULL(MAX(Card_Index),0) FROM #PersonCard

    /*---- 1.计算各段实际时间与有效范围 ----*/
    UPDATE #ClassSect SET InTimeDT=dbo.fnKQ_To24Hour(TimeIn,@CurDateDT),
                          OutTimeDT=dbo.fnKQ_To24Hour(TimeOut,@CurDateDT)
    UPDATE #ClassSect SET InTimeBgn=DATEADD(minute,IncBgnIn,InTimeDT),
                          InTimeEnd=DATEADD(minute,IncEndIn,InTimeDT),
                          OutTimeBgn=DATEADD(minute,IncBgnOut,OutTimeDT),
                          OutTimeEnd=DATEADD(minute,IncEndOut,OutTimeDT)
     WHERE InTimeDT IS NOT NULL AND OutTimeDT IS NOT NULL
    UPDATE #ClassSect SET InTimeEnd=DATEADD(minute,-@OvertimeBig,OutTimeDT)   -- 2009-09-23 加班段范围最大化
     WHERE Overtime=1 AND InTimeDT IS NOT NULL AND OutTimeDT IS NOT NULL
    UPDATE cs SET cs.InTimeEnd=cs.OutTimeBgn
      FROM #ClassSect cs
     WHERE cs.InTimeEnd IS NOT NULL AND cs.OutTimeBgn IS NOT NULL AND cs.OutTimeBgn>cs.InTimeEnd

    /*---- 2.逐段匹配上下班刷卡 ----*/
    SET @SectNo=1
    WHILE @SectNo<=5
    BEGIN
        SELECT @InTime=InTimeDT, @OutTime=OutTimeDT FROM #ClassSect WHERE SectNo=@SectNo
        IF (@InTime IS NULL OR @OutTime IS NULL) AND @Mode<>3 BREAK      -- 段次时间为空退出

        SET @RangeSect = CASE WHEN @Mode=3 THEN 1 ELSE @SectNo END       -- "只算刷卡时间"用第1段范围
        SELECT @RInBgn=InTimeBgn, @RInEnd=InTimeEnd, @ROutBgn=OutTimeBgn, @ROutEnd=OutTimeEnd,
               @InTime=InTimeDT, @OutTime=OutTimeDT, @InCard=InCard, @OutCard=OutCard, @Overtime=Overtime
          FROM #ClassSect WHERE SectNo=@RangeSect
        SET @WriteSect=@SectNo

        IF @Mode<>4 AND @RInBgn IS NOT NULL AND @ROutEnd IS NOT NULL
        BEGIN
            /*======== 匹配上班刷卡 fnMatchBrushInCard ========*/
            IF @Mode=3 OR @InCard<>0
            BEGIN
                SELECT @i=RecIndex+1 FROM #Ctx WHERE Id=1
                WHILE @i<=@MaxIdx
                BEGIN
                    SELECT @BrushDT=Brush_TimeDT, @DataFlag=ISNULL(Data_Flag,0)
                      FROM #PersonCard WHERE Card_Index=@i
                    IF @DataFlag NOT IN (12,14)                          -- 排除门禁等记录
                    BEGIN
                        IF @Mode=3 AND @BrushDT BETWEEN @RInBgn AND @ROutEnd
                        BEGIN
                            UPDATE #ClassSect SET InFact=@BrushDT WHERE SectNo=@WriteSect
                            SET @i=@i+1 BREAK
                        END
                        IF @BrushDT BETWEEN @RInBgn AND @RInEnd
                        BEGIN
                            SET @li_Minute=dbo.fnKQ_MinutesBetween(@BrushDT,@InTime)
                            IF NOT (@WriteSect<>1 AND @Overtime=1 AND @InCard=2 AND @li_Minute>=@OvertimeBig)
                            BEGIN
                                IF @InCard=1 OR (@InCard=2 AND @li_Minute<=@Late3E)
                                BEGIN
                                    UPDATE #ClassSect SET InFact=@BrushDT WHERE SectNo=@WriteSect
                                    SET @i=@i+1
                                END
                                BREAK
                            END
                            BREAK                                        -- 可能是加班段的下班卡
                        END
                    END
                    IF @BrushDT>=@ROutBgn BREAK                          -- 可能是下班刷卡
                    SET @i=@i+1
                END
                UPDATE #Ctx SET RecIndex=@i-1 WHERE Id=1
            END

            /*======== 匹配下班刷卡 fnMathcBrushOutCard ========*/
            IF @Mode=3 OR @OutCard<>0
            BEGIN
                SET @ld_OutTime=NULL
                SELECT @i=RecIndex+1 FROM #Ctx WHERE Id=1
                WHILE @i<=@MaxIdx
                BEGIN
                    SELECT @BrushDT=Brush_TimeDT FROM #PersonCard WHERE Card_Index=@i
                    IF @Mode=3 AND @BrushDT BETWEEN @RInBgn AND @ROutEnd
                    BEGIN
                        UPDATE #ClassSect SET OutFact=@BrushDT WHERE SectNo=@WriteSect
                        SET @i=@i+1 BREAK
                    END
                    IF @BrushDT>@ROutEnd BREAK
                    IF @Overtime=0                                       -- 正常段
                    BEGIN
                        IF @BrushDT>=@OutTime
                        BEGIN   -- 取最近一笔, 后一笔优先 vOutAddMinute 分钟
                            IF (@ld_OutTime IS NULL OR @ld_OutTime<@ROutBgn)
                               OR (dbo.fnKQ_MinutesBetween(@OutTime,@ld_OutTime)
                                   >= dbo.fnKQ_MinutesBetween(@BrushDT,@OutTime)-@OutAddMinute)
                            BEGIN SET @ld_OutTime=@BrushDT SET @i=@i+1 END
                            BREAK
                        END
                        ELSE
                        BEGIN   -- 与原程序一致(见2013-08-08注释处逻辑)
                            SET @li_Minute=dbo.fnKQ_MinutesBetween(@BrushDT,@InTime)
                            IF @li_Minute>@Late3E SET @ld_OutTime=@BrushDT
                        END
                    END
                    ELSE                                                 -- 加班段
                    BEGIN
                        SET @li_Minute=dbo.fnKQ_MinutesBetween(@BrushDT,@InTime)
                        IF @li_Minute>0
                        BEGIN
                            SET @ld_OutTime=@BrushDT
                            IF @li_Minute>=@OvertimeBig BEGIN SET @i=@i+1 BREAK END
                        END
                    END
                    SET @i=@i+1
                END
                /* 只有打下班卡没有打上班卡的处理 */
                SELECT @RInCard=InCard, @RInFact=InFact FROM #ClassSect WHERE SectNo=@RangeSect
                IF @ld_OutTime IS NOT NULL
                BEGIN
                    IF @Overtime=1 AND @RInCard=1 AND @RInFact IS NULL
                    BEGIN IF @i>1 SET @i=@i-1 END
                    ELSE UPDATE #ClassSect SET OutFact=@ld_OutTime WHERE SectNo=@WriteSect
                END
                UPDATE #Ctx SET RecIndex=@i-1 WHERE Id=1
            END
        END
        SET @SectNo=@SectNo+1
    END

    /*---- 3.自动补卡 fnAutoFillCard (从第5段往前) ----*/
    IF @Mode<>4
    BEGIN
        SET @CardState=0                -- 0无卡 1有上班卡 2有下班卡
        SET @SectNo=5
        WHILE @SectNo>=1
        BEGIN
            SELECT @sOutTime=OutTimeDT, @sOutFact=OutFact, @sInTime=InTimeDT, @sInFact=InFact,
                   @sInCard=InCard, @sOutCard=OutCard
              FROM #ClassSect WHERE SectNo=@SectNo
            /* 自动补下班卡 */
            IF @sOutTime IS NOT NULL
            BEGIN
                IF @sOutFact IS NOT NULL SET @CardState=2
                ELSE IF @sOutCard=0
                BEGIN
                    IF (@sInCard=0) OR (@sInFact IS NOT NULL) OR (@CardState>0)
                    BEGIN UPDATE #ClassSect SET OutFact=@sOutTime WHERE SectNo=@SectNo SET @CardState=2 END
                END
                ELSE IF @sOutCard=2 AND @CardState>0
                BEGIN
                    SET @j=@SectNo
                    WHILE @j>=1
                    BEGIN
                        IF EXISTS(SELECT 1 FROM #ClassSect WHERE SectNo=@j AND InFact IS NOT NULL)
                           OR (@j>1 AND EXISTS(SELECT 1 FROM #ClassSect WHERE SectNo=@j-1 AND OutFact IS NOT NULL))
                        BEGIN UPDATE #ClassSect SET OutFact=@sOutTime WHERE SectNo=@SectNo BREAK END
                        SET @j=@j-1
                    END
                END
            END
            /* 自动补上班卡 */
            IF @sInTime IS NOT NULL
            BEGIN
                IF @sInFact IS NOT NULL SET @CardState=1
                ELSE IF @sInCard<>1
                   AND EXISTS(SELECT 1 FROM #ClassSect WHERE SectNo=@SectNo AND OutFact IS NOT NULL)
                BEGIN UPDATE #ClassSect SET InFact=@sInTime WHERE SectNo=@SectNo SET @CardState=1 END
            END
            SET @SectNo=@SectNo-1
        END
    END
END
GO

/*====================== 4. 内部过程: 休息处理 (fnDealWithRestMode) ======================*/
IF OBJECT_ID('dbo.KQ_pd_RestOneDay') IS NOT NULL DROP PROC dbo.KQ_pd_RestOneDay
GO
CREATE PROC dbo.KQ_pd_RestOneDay @CurDate VARCHAR(10)      -- fnRestOneDay: 休息整天
AS
BEGIN
    DECLARE @v DECIMAL(12,2), @TxtFillClass NVARCHAR(10)
    SELECT @TxtFillClass=TxtFillClass FROM #Ctx WHERE Id=1
    IF EXISTS(SELECT 1 FROM #AdjRest WHERE Adjust_Date=@CurDate AND Adjust_Class=1)   -- 有补班记录
    BEGIN
        SELECT @v=ISNULL(Adjust_Hour,0) FROM #AdjRest WHERE Adjust_Date=@CurDate AND Adjust_Class=1
        IF @v<>0
        BEGIN
            UPDATE #Ctx SET OvertimeHour=CASE WHEN FactWork>@v*60 THEN OvertimeHour+FactWork-@v*60 ELSE OvertimeHour END,
                   FactWork=CASE WHEN FactWork>@v*60 THEN @v*60 ELSE FactWork END,
                   MustWork=@v*60, OvertimeClass='X' WHERE Id=1
        END
        ELSE UPDATE #Ctx SET OvertimeClass='P' WHERE Id=1
        UPDATE #Ctx SET Reason=Reason+N','+@TxtFillClass WHERE Id=1
    END
    ELSE
        UPDATE #Ctx SET OvertimeHour=OvertimeHour+FactWork, OvertimeClass='X', MustWork=0, FactWork=0,
               EarlyMinute=0, LateMinute=0, LateData='', EarlyData='', AbsentWork=0 WHERE Id=1
END
GO
IF OBJECT_ID('dbo.KQ_pd_RestHalfDay') IS NOT NULL DROP PROC dbo.KQ_pd_RestHalfDay
GO
CREATE PROC dbo.KQ_pd_RestHalfDay @CurDate VARCHAR(10)     -- fnRestHalfDay: 休息半天
AS
BEGIN
    DECLARE @v DECIMAL(12,2), @TxtFillClass NVARCHAR(10),
            @Sect1Must DECIMAL(12,2), @Sect1Fact DECIMAL(12,2), @Must DECIMAL(12,2)
    SELECT @TxtFillClass=TxtFillClass, @Sect1Must=Sect1Must, @Sect1Fact=Sect1Fact FROM #Ctx WHERE Id=1
    IF EXISTS(SELECT 1 FROM #AdjRest WHERE Adjust_Date=@CurDate AND Adjust_Class=1)
    BEGIN
        SELECT @v=ISNULL(Adjust_Hour,0) FROM #AdjRest WHERE Adjust_Date=@CurDate AND Adjust_Class=1
        IF @v<>0
        BEGIN
            SET @Must=@Sect1Must+@v*60
            UPDATE #Ctx SET OvertimeHour=CASE WHEN FactWork>@Must THEN OvertimeHour+FactWork-@Must ELSE OvertimeHour END,
                   FactWork=CASE WHEN FactWork>@Must THEN @Must ELSE FactWork END,
                   MustWork=@Must, OvertimeClass='X' WHERE Id=1
        END
        ELSE UPDATE #Ctx SET OvertimeClass='P' WHERE Id=1
        UPDATE #Ctx SET Reason=Reason+N','+@TxtFillClass WHERE Id=1
    END
    ELSE
    BEGIN
        UPDATE #Ctx SET MustWork=@Sect1Must WHERE Id=1
        UPDATE #Ctx SET OvertimeHour=OvertimeHour+(FactWork-@Sect1Fact), FactWork=@Sect1Fact
         WHERE Id=1 AND FactWork>@Sect1Must                -- 注: 与原程序一致, 此处用 Sect1Fact
    END
    UPDATE #Ctx SET LateMinute=Sect1LateMinute, EarlyMinute=Sect1EarlyMinute,
           LateData=Sect1LateData, EarlyData=Sect1EarlyData WHERE Id=1
END
GO
IF OBJECT_ID('dbo.KQ_pd_DealRestMode') IS NOT NULL DROP PROC dbo.KQ_pd_DealRestMode
GO
CREATE PROC dbo.KQ_pd_DealRestMode @CurDate VARCHAR(10), @CurDateDT DATETIME
AS
BEGIN
    DECLARE @RestMode INT, @RestDays VARCHAR(60), @OTMode INT, @TxtFillClass NVARCHAR(10),
            @HolidayName NVARCHAR(50), @isHoliday INT, @OfWeek VARCHAR(1), @DayNo VARCHAR(2), @p INT
    SELECT @RestMode=ISNULL(Rest_Mode,0), @RestDays=ISNULL(Rest_Days,''), @OTMode=Overtime_Mode,
           @TxtFillClass=TxtFillClass FROM #Ctx WHERE Id=1

    /* 法定假日判断 (KQ_Holiday.Holiday_Date 存 MM-DD) */
    SET @isHoliday=0
    SELECT @HolidayName=Holiday_Name FROM KQ_Holiday WHERE Holiday_Date=SUBSTRING(@CurDate,6,5)
    IF @HolidayName IS NOT NULL
    BEGIN
        SET @isHoliday=1
        IF NOT EXISTS(SELECT 1 FROM #AdjRest WHERE Adjust_Date=@CurDate AND Adjust_Class=1)
        BEGIN
            UPDATE #Ctx SET OvertimeHour=OvertimeHour+FactWork, OvertimeClass='J', MustWork=0, FactWork=0,
                   EarlyMinute=0, LateMinute=0, LateData='', EarlyData='', AbsentWork=0 WHERE Id=1
            UPDATE #Ctx SET Reason=Reason+N','+@HolidayName WHERE Id=1
            RETURN
        END
    END

    /* 是否休息日 (1=按星期, 2=按日期; 带.5表示半天) */
    SET @OfWeek=CAST((DATEPART(weekday,@CurDateDT)+@@DATEFIRST-2)%7+1 AS VARCHAR(1))   -- 周一=1..周日=7
    SET @DayNo=SUBSTRING(@CurDate,9,2)
    SET @p=CHARINDEX(@OfWeek,@RestDays)
    IF (@RestMode=1 AND @p>0 AND (@p=1 OR SUBSTRING(@RestDays,@p-1,1)<>'.'))
       OR (@RestMode=2 AND CHARINDEX(@DayNo,@RestDays)>0)
       OR @isHoliday=1
    BEGIN
        IF @isHoliday=1
        BEGIN
            EXEC dbo.KQ_pd_RestOneDay @CurDate
            UPDATE #Ctx SET OvertimeClass='J' WHERE Id=1
        END
        ELSE
        BEGIN
            UPDATE #Ctx SET OvertimeClass='X' WHERE Id=1
            IF @RestMode=1 AND @p>0 AND SUBSTRING(@RestDays,@p+1,1)='.'
                 EXEC dbo.KQ_pd_RestHalfDay @CurDate
            ELSE EXEC dbo.KQ_pd_RestOneDay @CurDate
        END
        IF @OTMode=4 UPDATE #Ctx SET OvertimeHour=0 WHERE Id=1          -- 休息日不加班
    END
    ELSE UPDATE #Ctx SET OvertimeClass='P' WHERE Id=1                   -- 平时加班
END
GO

/*====================== 5. 内部过程: 请假处理 (fnDealWithLeaveData) ======================*/
IF OBJECT_ID('dbo.KQ_pd_DealLeaveData') IS NOT NULL DROP PROC dbo.KQ_pd_DealLeaveData
GO
CREATE PROC dbo.KQ_pd_DealLeaveData @CurDate VARCHAR(10)
AS
BEGIN
    DECLARE @Kind_No VARCHAR(20), @Kind_Name NVARCHAR(50), @Leave_Hour DECIMAL(12,2),
            @is_Deduct BIT, @is_OutLeave BIT,
            @li_Value DECIMAL(12,2), @li_Value0 DECIMAL(12,2), @Valuex DECIMAL(12,2),
            @Must DECIMAL(12,2), @Fact DECIMAL(12,2), @Absent DECIMAL(12,2), @Over DECIMAL(12,2),
            @LeaveHour DECIMAL(12,2), @LeaveData VARCHAR(200), @LeaveName NVARCHAR(200),
            @LateMinute INT, @EarlyMinute INT, @LeaveDatum INT, @IsDeductLE INT
    SELECT @LeaveDatum=LeaveDatum, @IsDeductLE=IsDeductLE FROM #Ctx WHERE Id=1

    DECLARE cur_Leave CURSOR LOCAL FAST_FORWARD FOR
        SELECT Kind_No, Kind_Name, Leave_Hour, Is_Deduct, IsOutLeave
          FROM #Leave WHERE Leave_Date=@CurDate
    OPEN cur_Leave
    FETCH NEXT FROM cur_Leave INTO @Kind_No,@Kind_Name,@Leave_Hour,@is_Deduct,@is_OutLeave
    WHILE @@FETCH_STATUS=0
    BEGIN
        SELECT @Must=MustWork, @Fact=FactWork, @Absent=AbsentWork, @Over=OverAbsent,
               @LeaveHour=LeaveHour, @LeaveData=LeaveData, @LeaveName=LeaveName,
               @LateMinute=LateMinute, @EarlyMinute=EarlyMinute FROM #Ctx WHERE Id=1

        SET @li_Value=@Leave_Hour
        IF @li_Value=0 SET @li_Value=@Must               -- 请假小时=0 视为请假整天
        SET @li_Value0=@li_Value

        IF @Absent>0
        BEGIN
            IF @Over>0                                   -- 优先抵扣未加班旷工
            BEGIN
                IF @li_Value>=@Over
                BEGIN SET @li_Value=@li_Value-@Over SET @Absent=@Absent-@Over SET @Over=0 END
                ELSE BEGIN SET @Absent=@Absent-@li_Value SET @Over=@Over-@li_Value SET @li_Value=0 END
            END
            IF @li_Value>0
            BEGIN
                IF @li_Value>@Absent AND @LeaveDatum=0
                BEGIN SET @li_Value=@Absent SET @li_Value0=@li_Value END
                IF @is_Deduct=1
                BEGIN
                    IF @is_OutLeave=1 SET @Fact=@Fact-@li_Value
                    ELSE SET @Absent=@Absent-@li_Value
                END
                ELSE IF @is_OutLeave=0
                BEGIN SET @Absent=@Absent-@li_Value SET @Fact=@Fact+@li_Value END

                /* fnLeaveOutLateEarly(-Absent): 请假抵扣早退迟到 */
                IF @Absent<=0 AND @IsDeductLE=1
                   AND ((@EarlyMinute+@Absent*60>=0) OR (ABS(@LateMinute+@EarlyMinute+@Absent*60)<=60))
                BEGIN
                    SET @Valuex=FLOOR(ABS(@Absent)*60)
                    SET @Fact=@Fact-CAST(ROUND((@LateMinute+@EarlyMinute)/60.0,1) AS DECIMAL(12,2))
                    IF @LateMinute>0 AND @Valuex>0
                    BEGIN
                        SET @LateMinute=@LateMinute-@Valuex
                        IF @LateMinute<0 BEGIN SET @Valuex=-@LateMinute SET @LateMinute=0 END ELSE SET @Valuex=0
                    END
                    IF @EarlyMinute>0 AND @Valuex>0
                    BEGIN
                        SET @EarlyMinute=@EarlyMinute-@Valuex
                        IF @EarlyMinute<0 BEGIN SET @Valuex=-@EarlyMinute SET @EarlyMinute=0 END ELSE SET @Valuex=0
                    END
                    IF @Valuex>0 SET @Absent=-CAST(ROUND(@Valuex/60.0,1) AS DECIMAL(12,2))
                    ELSE SET @Absent=0
                END

                IF @Absent<0 AND @LeaveDatum=1 SET @Fact=@Fact+@Absent
            END
        END
        ELSE
        BEGIN
            IF @IsDeductLE=1                             -- fnLeaveOutLateEarly(li_Value)
            BEGIN
                SET @Valuex=FLOOR(@li_Value*60)
                SET @Fact=@Fact-CAST(ROUND((@LateMinute+@EarlyMinute)/60.0,1) AS DECIMAL(12,2))
                IF @LateMinute>0 AND @Valuex>0
                BEGIN
                    SET @LateMinute=@LateMinute-@Valuex
                    IF @LateMinute<0 BEGIN SET @Valuex=-@LateMinute SET @LateMinute=0 END ELSE SET @Valuex=0
                END
                IF @EarlyMinute>0 AND @Valuex>0
                BEGIN
                    SET @EarlyMinute=@EarlyMinute-@Valuex
                    IF @EarlyMinute<0 BEGIN SET @Valuex=-@EarlyMinute SET @EarlyMinute=0 END ELSE SET @Valuex=0
                END
                IF @Valuex>0 SET @Absent=-CAST(ROUND(@Valuex/60.0,1) AS DECIMAL(12,2))
                ELSE SET @Absent=0
            END
            IF @is_Deduct=1 SET @Fact=@Fact-@li_Value
        END

        IF @Absent<0 SET @Absent=0                       -- 与原程序一致
        IF @Over<0 SET @Over=0
        IF @Fact>@Must SET @Fact=@Must

        SET @LeaveHour=@LeaveHour+@li_Value0
        SET @LeaveData=@LeaveData+@Kind_No+'/'+CAST(@li_Value0 AS VARCHAR(20))+','
        SET @LeaveName=@LeaveName+@Kind_Name+','

        UPDATE #Ctx SET MustWork=@Must, FactWork=@Fact, AbsentWork=@Absent, OverAbsent=@Over,
               LateMinute=@LateMinute, EarlyMinute=@EarlyMinute,
               LeaveHour=@LeaveHour, LeaveData=@LeaveData, LeaveName=@LeaveName WHERE Id=1

        FETCH NEXT FROM cur_Leave INTO @Kind_No,@Kind_Name,@Leave_Hour,@is_Deduct,@is_OutLeave
    END
    CLOSE cur_Leave DEALLOCATE cur_Leave
END
GO

/*====================== 6. 内部过程: 其它异常 (fnCalcOtherException) ======================*/
IF OBJECT_ID('dbo.KQ_pd_DealOtherException') IS NOT NULL DROP PROC dbo.KQ_pd_DealOtherException
GO
CREATE PROC dbo.KQ_pd_DealOtherException @CurDate VARCHAR(10)
AS
BEGIN
    DECLARE @v DECIMAL(12,2), @minOT DECIMAL(12,2), @OTMode INT, @OvertimeDatum INT,
            @TxtFillAttend NVARCHAR(10), @TxtAdjustRest NVARCHAR(10)
    SELECT @OTMode=Overtime_Mode, @OvertimeDatum=OvertimeDatum,
           @TxtFillAttend=TxtFillAttend, @TxtAdjustRest=TxtAdjustRest FROM #Ctx WHERE Id=1

    /*1.补出勤*/
    IF (SELECT AbsentWork FROM #Ctx WHERE Id=1)>0
       AND EXISTS(SELECT 1 FROM #FillAttend WHERE Fill_Date=@CurDate AND Fill_Class=0)
    BEGIN
        SELECT TOP 1 @v=ISNULL(Fill_Hour,0) FROM #FillAttend WHERE Fill_Date=@CurDate AND Fill_Class=0
        UPDATE #Ctx SET FactWork=CASE WHEN FactWork+@v>MustWork THEN MustWork ELSE FactWork+@v END,
               AbsentWork=AbsentWork-@v, Reason=Reason+N','+@TxtFillAttend WHERE Id=1
    END
    /*2.调休*/
    IF EXISTS(SELECT 1 FROM #AdjRest WHERE Adjust_Date=@CurDate AND Adjust_Class=0)
    BEGIN
        SELECT TOP 1 @v=ISNULL(Adjust_Hour,0) FROM #AdjRest WHERE Adjust_Date=@CurDate AND Adjust_Class=0
        IF @v=0 OR @v>=(SELECT MustWork FROM #Ctx WHERE Id=1)             -- 调休整天
        BEGIN
            UPDATE #Ctx SET OvertimeHour=OvertimeHour+FactWork, OvertimeClass='X', MustWork=0, FactWork=0,
                   EarlyMinute=0, LateMinute=0, LateData='', EarlyData='', AbsentWork=0 WHERE Id=1
            IF @OTMode=4 UPDATE #Ctx SET OvertimeHour=0 WHERE Id=1
        END
        ELSE                                                             -- 调休几个小时
        BEGIN
            UPDATE #Ctx SET MustWork=MustWork-@v WHERE Id=1
            UPDATE #Ctx SET MustWork=0, OvertimeClass='X' WHERE Id=1 AND MustWork<=0
            UPDATE #Ctx SET
                   OvertimeHour=CASE WHEN FactWork>MustWork THEN CASE WHEN @OTMode<4 THEN OvertimeHour+FactWork-MustWork ELSE 0 END ELSE OvertimeHour END,
                   FactWork=CASE WHEN FactWork>MustWork THEN MustWork ELSE FactWork END,
                   AbsentWork=AbsentWork-@v WHERE Id=1
        END
        UPDATE #Ctx SET Reason=Reason+N','+@TxtAdjustRest WHERE Id=1
    END
    /*3.加班条 (加班以加班条为准 或 加班方式=必须申请)*/
    IF @OvertimeDatum=1 OR @OTMode=6
    BEGIN
        IF EXISTS(SELECT 1 FROM #OverTime WHERE Over_Date=@CurDate)
        BEGIN
            SELECT TOP 1 @minOT=Over_Hour FROM #OverTime WHERE Over_Date=@CurDate
            UPDATE #Ctx SET OvertimeHour=CASE WHEN OvertimeHour<@minOT THEN OvertimeHour ELSE @minOT END WHERE Id=1
        END
        ELSE UPDATE #Ctx SET OvertimeHour=0 WHERE Id=1
    END
    /*4.补加班*/
    IF EXISTS(SELECT 1 FROM #FillAttend WHERE Fill_Date=@CurDate AND Fill_Class=1)
    BEGIN
        SELECT TOP 1 @v=ISNULL(Fill_Hour,0) FROM #FillAttend WHERE Fill_Date=@CurDate AND Fill_Class=1
        UPDATE #Ctx SET OvertimeHour=OvertimeHour+@v WHERE Id=1
    END
    /*应出勤=0 且 加班>0 且 平时 → 休息日加班*/
    UPDATE #Ctx SET OvertimeClass='X' WHERE Id=1 AND MustWork=0 AND OvertimeHour>0 AND OvertimeClass='P'
END
GO

/*====================== 7. 内部过程: 灵活班次评分 (fnCalcClassScore) ======================*/
IF OBJECT_ID('dbo.KQ_pd_CalcClassScore') IS NOT NULL DROP PROC dbo.KQ_pd_CalcClassScore
GO
CREATE PROC dbo.KQ_pd_CalcClassScore @ClassNo VARCHAR(20), @PriorClassNo VARCHAR(20),
                                     @Score DECIMAL(12,2) OUTPUT
AS
BEGIN
    DECLARE @Vaild INT, @Must INT, @li_Minute DECIMAL(12,4), @Done INT,
            @InTime DATETIME, @OutTime DATETIME, @InFact DATETIME, @OutFact DATETIME,
            @InCard INT, @OutCard INT, @Overtime BIT, @ClassName NVARCHAR(50)
    SELECT @Vaild=0, @Must=0, @Score=0, @Done=0

    DECLARE cur_S CURSOR LOCAL FAST_FORWARD FOR
        SELECT InTimeDT, OutTimeDT, InFact, OutFact, InCard, OutCard, Overtime
          FROM #ClassSect WHERE InTimeDT IS NOT NULL ORDER BY SectNo
    OPEN cur_S
    FETCH NEXT FROM cur_S INTO @InTime,@OutTime,@InFact,@OutFact,@InCard,@OutCard,@Overtime
    WHILE @@FETCH_STATUS=0 AND @Done=0
    BEGIN
        /*---- 上班得分 fnCalcInScore ----*/
        SET @Must=@Must+1
        IF @InFact IS NOT NULL
        BEGIN
            SET @Score=@Score+10 SET @Vaild=@Vaild+1
            IF @InCard=1 SET @Score=@Score-2
            ELSE IF @InCard=2 AND @InFact=@InTime SET @Score=@Score-1
            SET @li_Minute=dbo.fnKQ_MinutesBetween(@InFact,@InTime)
            IF @li_Minute>0
            BEGIN
                IF @Overtime=1 AND @li_Minute>90 SET @Score=@Score-6
                IF @li_Minute>10 AND @li_Minute<=60
                BEGIN
                    SET @Vaild=@Vaild-1
                    IF @li_Minute<=20 SET @li_Minute=-(@li_Minute/10.0)
                    ELSE SET @li_Minute=-(@li_Minute/15.0)
                END
                ELSE SET @li_Minute=-1
                SET @Score=@Score+@li_Minute
            END
        END
        IF @OutTime IS NULL SET @Done=1
        ELSE
        BEGIN
            /*---- 下班得分 fnCalcOutScore ----*/
            SET @Must=@Must+1
            IF @OutFact IS NOT NULL
            BEGIN
                SET @Score=@Score+8 SET @Vaild=@Vaild+1
                IF @OutCard=1 SET @Score=@Score-2
                ELSE IF @OutCard=2 AND @OutFact=@OutTime SET @Score=@Score-1  -- 原程序此处比较写错, 按准点扣分处理
                SET @li_Minute=dbo.fnKQ_MinutesBetween(@OutTime,@OutFact)
                IF @li_Minute>0
                BEGIN
                    IF @li_Minute>10 SET @Vaild=@Vaild-1
                    IF @li_Minute>5 AND @li_Minute<=30
                    BEGIN
                        IF @li_Minute<=10 SET @li_Minute=-(@li_Minute/5.0)
                        ELSE SET @li_Minute=-(@li_Minute/10.0)
                    END
                    ELSE SET @li_Minute=-1
                    SET @Score=@Score+@li_Minute
                END
            END
            FETCH NEXT FROM cur_S INTO @InTime,@OutTime,@InFact,@OutFact,@InCard,@OutCard,@Overtime
        END
    END
    CLOSE cur_S DEALLOCATE cur_S

    IF @Must>0 SET @Score=@Score+(@Vaild*1.0/@Must)*5          -- 合法刷卡得分
    IF @ClassNo=@PriorClassNo SET @Score=@Score+2              -- 与前一天同班
    SET @Score=ROUND(@Score,2)
    SELECT @ClassName=Class_Name FROM KQ_Class WHERE Class_No=@ClassNo
    IF @Must=0 OR CHARINDEX('X',@ClassName)>0 SET @Score=5.8   -- 灵活中的自动休息日
END
GO

/*====================== 8. 内部过程: 规律班次 (fnSetRuleClassInfo / fnGetDateClassNo) ======================*/
IF OBJECT_ID('dbo.KQ_pd_SetRuleClassInfo') IS NOT NULL DROP PROC dbo.KQ_pd_SetRuleClassInfo
GO
CREATE PROC dbo.KQ_pd_SetRuleClassInfo @ClassNo VARCHAR(20)      -- 返回1成功 / 0失败
AS
BEGIN
    DECLARE @Mode INT, @Cycle INT, @Start VARCHAR(10), @acc INT, @bgn INT, @days INT
    TRUNCATE TABLE #RuleSub
    SELECT @Mode=Rule_Mode, @Cycle=Rule_Cycle, @Start=Start_Date
      FROM KQ_RuleClass WHERE Class_No=@ClassNo
    IF @@ROWCOUNT=0 RETURN 0
    INSERT INTO #RuleSub (Shift_ClassNo, Shift_Days)
    SELECT Shift_ClassNo, Shift_Days FROM KQ_RuleClassSub WHERE Class_No=@ClassNo ORDER BY Shift_ClassNo
    IF NOT EXISTS(SELECT 1 FROM #RuleSub) RETURN 0
    SET @acc=0
    DECLARE cur_R CURSOR LOCAL FAST_FORWARD FOR SELECT Shift_Days FROM #RuleSub ORDER BY Seq
    OPEN cur_R FETCH NEXT FROM cur_R INTO @days
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @bgn=@acc SET @acc=@acc+@days
        UPDATE #RuleSub SET RangeBgn=@bgn, RangeEnd=@acc WHERE CURRENT OF cur_R
        FETCH NEXT FROM cur_R INTO @days
    END
    CLOSE cur_R DEALLOCATE cur_R
    UPDATE #Ctx SET RuleMode=@Mode, RuleCycle=@acc, RuleStart=CAST(@Start AS DATETIME) WHERE Id=1
    RETURN 1
END
GO
IF OBJECT_ID('dbo.KQ_pd_GetRuleClassNo') IS NOT NULL DROP PROC dbo.KQ_pd_GetRuleClassNo
GO
CREATE PROC dbo.KQ_pd_GetRuleClassNo @DayClassNo VARCHAR(20) OUTPUT   -- fnGetDateClassNo
AS
BEGIN
    DECLARE @CurDateDT DATETIME, @Mode INT, @Cycle INT, @Start DATETIME, @liMod INT
    SELECT @CurDateDT=CurDateDT, @Mode=RuleMode, @Cycle=RuleCycle, @Start=RuleStart FROM #Ctx WHERE Id=1
    IF @Mode=0 SET @liMod=DATEDIFF(day,@Start,@CurDateDT)                        -- 按天数轮班
    ELSE IF @Mode=2 SET @liMod=DATEDIFF(day,DATEADD(month,DATEDIFF(month,0,@CurDateDT),0),@CurDateDT)  -- 月内轮班
    ELSE SET @liMod=MONTH(@CurDateDT)-MONTH(@Start)                              -- 按月数轮班
    SET @liMod=@liMod % @Cycle
    IF @liMod<0 SET @liMod=@liMod+@Cycle
    SELECT @DayClassNo=Shift_ClassNo FROM #RuleSub WHERE @liMod>=RangeBgn AND @liMod<RangeEnd
END
GO

/*====================== 9. 内部过程: 计算并写入一天数据 (fnCalcDailyData + fnInsertOneDateData) ======================*/
IF OBJECT_ID('dbo.KQ_pd_CalcOneDay') IS NOT NULL DROP PROC dbo.KQ_pd_CalcOneDay
GO
CREATE PROC dbo.KQ_pd_CalcOneDay @CurDate VARCHAR(10), @CurDateDT DATETIME
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @Mode INT, @OTMode INT,
            @Late1S INT,@Late1E INT,@Late2E INT,@Late3E INT,
            @Early1S INT,@Early1E INT,@Early2E INT,@Early3E INT,
            @MinToHour INT,@OneHour INT,@HalfHour INT,@Decimals INT,
            @InTime DATETIME,@OutTime DATETIME,@InFact DATETIME,@OutFact DATETIME,
            @InCard INT,@OutCard INT,@Deduct INT,@Overtime BIT,
            @ld_InFact DATETIME,@ld_OutFact DATETIME,@li_Minute INT,
            @li_SectMust DECIMAL(12,2),@li_SectFact DECIMAL(12,2),@li_SectOT DECIMAL(12,2),@FactSum DECIMAL(12,2),
            @li_SectLate INT,@li_SectEarly INT,@ls_SectLate VARCHAR(30),@ls_SectEarly VARCHAR(30),
            @OvertimeClass VARCHAR(2),@AbsentWork DECIMAL(12,2),@OvertimeHour DECIMAL(12,2),
            @Reason NVARCHAR(200),@LateMinute INT,@EarlyMinute INT,@LeaveHour DECIMAL(12,2),
            @TxtAbsent NVARCHAR(10),@TxtLeave NVARCHAR(10),@TxtLate NVARCHAR(10),
            @TxtEarly NVARCHAR(10),@TxtForget NVARCHAR(10),@BCardForget INT,
            @SectNo INT,@AnyCard INT,@BCardForgetFlag INT,
            @S1In VARCHAR(16),@S1Out VARCHAR(16),@S2In VARCHAR(16),@S2Out VARCHAR(16),
            @S3In VARCHAR(16),@S3Out VARCHAR(16),@S4In VARCHAR(16),@S4Out VARCHAR(16),
            @S5In VARCHAR(16),@S5Out VARCHAR(16),@OfWeek NVARCHAR(2),@BrushData VARCHAR(255)

    SELECT @Mode=Worktime_Mode, @OTMode=Overtime_Mode,
           @Late1S=Late1S,@Late1E=Late1E,@Late2E=Late2E,@Late3E=Late3E,
           @Early1S=Early1S,@Early1E=Early1E,@Early2E=Early2E,@Early3E=Early3E,
           @MinToHour=MinToHour,@OneHour=OneHour,@HalfHour=HalfHour,@Decimals=Decimals,
           @TxtAbsent=TxtAbsent,@TxtLeave=TxtLeave,@TxtLate=TxtLate,@TxtEarly=TxtEarly,
           @TxtForget=TxtForget,@BCardForget=BCardForget
      FROM #Ctx WHERE Id=1

    /*============== 1.按考勤方式计算 ==============*/
    IF @Mode=1      /* ---- 按班次刷卡 fnWorkInClass ---- */
    BEGIN
        SET @SectNo=1
        WHILE @SectNo<=5
        BEGIN
            SELECT @InTime=InTimeDT,@OutTime=OutTimeDT,@InFact=InFact,@OutFact=OutFact,
                   @Deduct=ISNULL(Deduct,0),@Overtime=Overtime
              FROM #ClassSect WHERE SectNo=@SectNo
            IF @@ROWCOUNT=0 OR @InTime IS NULL BREAK          -- 没有上班时间就退出

            SET @li_SectLate=0 SET @li_SectEarly=0 SET @ls_SectLate='' SET @ls_SectEarly=''
            SET @ld_InFact=@InFact SET @ld_OutFact=@OutFact

            IF @Overtime=0                                    -- 非加班段
            BEGIN
                SET @li_SectMust=dbo.fnKQ_MinutesBetween(@OutTime,@InTime)-@Deduct
                IF @InFact IS NOT NULL AND @OutFact IS NOT NULL
                BEGIN
                    /* 迟到 fnCalcLateData */
                    IF @InFact>@InTime
                    BEGIN
                        SET @li_Minute=dbo.fnKQ_MinutesBetween(@InFact,@InTime)
                        IF @li_Minute<=@Late3E
                        BEGIN
                            IF @li_Minute>=@Late1S
                            BEGIN
                                SET @li_SectLate=@li_Minute
                                IF @li_Minute<=@Late1E SET @ls_SectLate='1/'+CAST(@li_Minute AS VARCHAR)+','
                                ELSE IF @li_Minute<=@Late2E SET @ls_SectLate='2/'+CAST(@li_Minute AS VARCHAR)+','
                                ELSE SET @ls_SectLate='3/'+CAST(@li_Minute AS VARCHAR)+','
                            END
                            ELSE SET @ld_InFact=@InTime
                        END
                    END
                    ELSE SET @ld_InFact=@InTime
                    /* 早退 fnCalcEarlyData */
                    IF @OutFact<@OutTime
                    BEGIN
                        SET @li_Minute=dbo.fnKQ_MinutesBetween(@OutTime,@OutFact)
                        IF @li_Minute<=@Early3E
                        BEGIN
                            IF @li_Minute>=@Early1S
                            BEGIN
                                SET @li_SectEarly=@li_Minute
                                IF @li_Minute<=@Early1E SET @ls_SectEarly='1/'+CAST(@li_Minute AS VARCHAR)+','
                                ELSE IF @li_Minute<=@Early2E SET @ls_SectEarly='2/'+CAST(@li_Minute AS VARCHAR)+','
                                ELSE SET @ls_SectEarly='3/'+CAST(@li_Minute AS VARCHAR)+','
                            END
                            ELSE SET @ld_OutFact=@OutTime
                        END
                    END
                    ELSE SET @ld_OutFact=@OutTime
                    /* 段实出勤 */
                    SET @li_SectFact=dbo.fnKQ_MinutesBetween(@ld_OutFact,@ld_InFact)
                    SET @li_SectFact=dbo.fnKQ_FactWorkHour(@li_SectMust,@li_SectFact,@Deduct)
                END
                ELSE SET @li_SectFact=0

                /* 累加 fnAddUpDailyData */
                UPDATE #Ctx SET MustWork=MustWork+@li_SectMust, FactWork=FactWork+@li_SectFact,
                       LateMinute=LateMinute+@li_SectLate, LateData=LateData+@ls_SectLate,
                       EarlyMinute=EarlyMinute+@li_SectEarly, EarlyData=EarlyData+@ls_SectEarly
                 WHERE Id=1
                IF (SELECT Sect1Must FROM #Ctx WHERE Id=1)=0          -- 得到半天考勤数据
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM #ClassSect
                                   WHERE SectNo>@SectNo AND OutTimeDT IS NOT NULL AND Overtime=0)
                        UPDATE #Ctx SET Sect1Must=@li_SectMust/2.0, Sect1Fact=@li_SectFact/2.0,
                               Sect1EarlyMinute=0, Sect1EarlyData='' WHERE Id=1
                    ELSE
                        UPDATE #Ctx SET Sect1Must=@li_SectMust, Sect1Fact=@li_SectFact,
                               Sect1EarlyMinute=@li_SectEarly, Sect1EarlyData=@ls_SectEarly WHERE Id=1
                    UPDATE #Ctx SET Sect1LateMinute=@li_SectLate, Sect1LateData=@ls_SectLate WHERE Id=1
                END
            END
            ELSE                                              -- 加班段 fnCalcOvertimeData
            BEGIN
                SET @li_SectMust=dbo.fnKQ_MinutesBetween(@OutTime,@InTime)-@Deduct
                SET @li_SectOT=0
                IF @OTMode<>5 AND @InFact IS NOT NULL AND @OutFact IS NOT NULL
                BEGIN
                    IF @OTMode<>2
                    BEGIN
                        IF @InFact<@InTime SET @ld_InFact=@InTime
                        IF @OutFact>@OutTime SET @ld_OutFact=@OutTime
                    END
                    SET @li_SectOT=dbo.fnKQ_MinutesBetween(@ld_OutFact,@ld_InFact)
                    IF @OTMode<>4 SET @li_SectOT=dbo.fnKQ_FactWorkHour(@li_SectMust,@li_SectOT,@Deduct)
                END
                IF @OTMode=3                                  -- 必须加班否则旷工
                BEGIN
                    UPDATE #Ctx SET OverAbsent=OverAbsent+@li_SectMust-@li_SectOT WHERE Id=1
                    SET @li_SectOT=0
                END
                UPDATE #Ctx SET OvertimeHour=OvertimeHour+@li_SectOT WHERE Id=1
            END
            SET @SectNo=@SectNo+1
        END
    END
    ELSE IF @Mode=2  /* ---- 一天任刷一次卡 fnWorkInOneCard(True) ---- */
    BEGIN
        SET @AnyCard=0 SET @SectNo=1
        WHILE @SectNo<=5
        BEGIN
            SELECT @InTime=InTimeDT,@OutTime=OutTimeDT,@InFact=InFact,@OutFact=OutFact,
                   @Deduct=ISNULL(Deduct,0),@Overtime=Overtime,@InCard=InCard,@OutCard=OutCard
              FROM #ClassSect WHERE SectNo=@SectNo
            IF @@ROWCOUNT=0 OR @InTime IS NULL BREAK
            IF @Overtime=0
                UPDATE #Ctx SET MustWork=MustWork+(dbo.fnKQ_MinutesBetween(@OutTime,@InTime)-@Deduct) WHERE Id=1
            IF @AnyCard=0 AND (((@InCard<>2) AND (@InFact IS NOT NULL))
                            OR ((@OutCard<>2) AND (@OutFact IS NOT NULL))) SET @AnyCard=1
            SET @SectNo=@SectNo+1
        END
        IF @AnyCard=0 UPDATE #Ctx SET FactWork=0 WHERE Id=1
        ELSE UPDATE #Ctx SET FactWork=MustWork WHERE Id=1
    END
    ELSE IF @Mode=3  /* ---- 只计算刷卡时间 fnWorkInBrushTime ---- */
    BEGIN
        SELECT @FactSum=ISNULL(SUM(dbo.fnKQ_MinutesBetween(OutFact,InFact)),0)
          FROM #ClassSect WHERE InFact IS NOT NULL AND OutFact IS NOT NULL
        UPDATE #Ctx SET FactWork=@FactSum, MustWork=@FactSum WHERE Id=1
    END
    ELSE            /* ---- 免刷卡正常出勤 fnWorkInOneCard(False) ---- */
    BEGIN
        SET @SectNo=1
        WHILE @SectNo<=5
        BEGIN
            SELECT @InTime=InTimeDT,@OutTime=OutTimeDT,@Deduct=ISNULL(Deduct,0),@Overtime=Overtime
              FROM #ClassSect WHERE SectNo=@SectNo
            IF @@ROWCOUNT=0 OR @InTime IS NULL BREAK
            IF @Overtime=0
                UPDATE #Ctx SET MustWork=MustWork+(dbo.fnKQ_MinutesBetween(@OutTime,@InTime)-@Deduct) WHERE Id=1
            SET @SectNo=@SectNo+1
        END
        UPDATE #Ctx SET MustWork=dbo.fnKQ_FormatData(MustWork,@MinToHour,@OneHour,@HalfHour,@Decimals),
               FactWork=dbo.fnKQ_FormatData(MustWork,@MinToHour,@OneHour,@HalfHour,@Decimals),
               OvertimeHour=dbo.fnKQ_FormatData(OvertimeHour,@MinToHour,@OneHour,@HalfHour,@Decimals)
         WHERE Id=1
        UPDATE #Ctx SET AbsentWork=MustWork-FactWork WHERE Id=1
    END

    /*============== 2.考勤方式<>4 的公共处理 (fnCalcDailyData 后半) ==============*/
    IF @Mode<>4
    BEGIN
        IF @Mode<>3
        BEGIN
            EXEC dbo.KQ_pd_DealRestMode @CurDate, @CurDateDT         -- 按休息方式计算
            IF (SELECT Overtime_Mode FROM #Ctx WHERE Id=1)=5
                UPDATE #Ctx SET OvertimeHour=0 WHERE Id=1            -- 不算加班
        END
        /* 分钟转小时 */
        UPDATE #Ctx SET
               MustWork=dbo.fnKQ_FormatData(MustWork,@MinToHour,@OneHour,@HalfHour,@Decimals),
               FactWork=dbo.fnKQ_FormatData(FactWork,@MinToHour,@OneHour,@HalfHour,@Decimals),
               OvertimeHour=dbo.fnKQ_FormatData(OvertimeHour,@MinToHour,@OneHour,@HalfHour,@Decimals),
               OverAbsent=dbo.fnKQ_FormatData(OverAbsent,@MinToHour,@OneHour,@HalfHour,@Decimals)
         WHERE Id=1
        /* 旷工 */
        UPDATE #Ctx SET AbsentWork=MustWork-FactWork
                        -dbo.fnKQ_FormatData(LateMinute+EarlyMinute,@MinToHour,@OneHour,@HalfHour,@Decimals)
                        +OverAbsent WHERE Id=1
        UPDATE #Ctx SET AbsentWork=0 WHERE Id=1 AND AbsentWork<0
        /* 迟到早退不扣全勤 */
        UPDATE #Ctx SET FactWork=FactWork
                        +dbo.fnKQ_FormatData(LateMinute+EarlyMinute,@MinToHour,@OneHour,@HalfHour,@Decimals)
         WHERE Id=1 AND LateMinute+EarlyMinute>0 AND @MinToHour IS NOT NULL
           AND (SELECT IsDeducte FROM #Ctx WHERE Id=1)=0

        EXEC dbo.KQ_pd_DealLeaveData @CurDate                        -- 请假异常处理
        EXEC dbo.KQ_pd_DealOtherException @CurDate                   -- 其它异常

        UPDATE #Ctx SET FactWork=0    WHERE Id=1 AND FactWork<0
        UPDATE #Ctx SET MustWork=0    WHERE Id=1 AND MustWork<0
        UPDATE #Ctx SET OvertimeHour=0 WHERE Id=1 AND OvertimeHour<0
        UPDATE #Ctx SET OverAbsent=0  WHERE Id=1 AND OverAbsent<0
        UPDATE #Ctx SET AbsentWork=0  WHERE Id=1 AND AbsentWork<0

        /* 平时加班作正常出勤 / 法定假上班作正常出勤 */
        SELECT @OvertimeClass=OvertimeClass FROM #Ctx WHERE Id=1
        IF (SELECT IsOverTimeAsWorkTime FROM #Ctx WHERE Id=1)=1 AND @OvertimeClass='P'
            UPDATE #Ctx SET MustWork=MustWork+OvertimeHour, FactWork=FactWork+OvertimeHour,
                   OvertimeHour=0, OvertimeClass='' WHERE Id=1
        SELECT @OvertimeClass=OvertimeClass FROM #Ctx WHERE Id=1
        IF (SELECT IsHolidayAsWorkTime FROM #Ctx WHERE Id=1)=1 AND @OvertimeClass='J'
            UPDATE #Ctx SET MustWork=OvertimeHour, FactWork=OvertimeHour,
                   OvertimeHour=0, OvertimeClass='', Reason='' WHERE Id=1
    END

    /*============== 3.写入一天结果 (fnInsertOneDateData) ==============*/
    SELECT @OvertimeClass=OvertimeClass, @AbsentWork=AbsentWork, @LeaveHour=LeaveHour,
           @LateMinute=LateMinute, @EarlyMinute=EarlyMinute, @Reason=Reason,
           @OvertimeHour=OvertimeHour
      FROM #Ctx WHERE Id=1

    IF @OvertimeHour<=0 SET @OvertimeClass=''
    IF @AbsentWork<=0 SET @AbsentWork=0 ELSE SET @Reason=@Reason+N','+@TxtAbsent
    IF @LeaveHour>0 SET @Reason=@Reason+N','+@TxtLeave
    IF @LateMinute>0 SET @Reason=@Reason+N','+@TxtLate
    IF @EarlyMinute>0 SET @Reason=@Reason+N','+@TxtEarly
    SET @Reason=SUBSTRING(@Reason,2,40)
    IF @OvertimeHour*60<=(SELECT OvertimeBig FROM #Ctx WHERE Id=1)
    BEGIN SET @OvertimeHour=0 SET @OvertimeClass='' END

    /* 各段实际时间字符串 + 漏刷卡标志 */
    SET @S1In=NULL SET @S1Out=NULL SET @S2In=NULL SET @S2Out=NULL
    SET @S3In=NULL SET @S3Out=NULL SET @S4In=NULL SET @S4Out=NULL
    SET @S5In=NULL SET @S5Out=NULL SET @BCardForgetFlag=0 SET @SectNo=1
    WHILE @SectNo<=5
    BEGIN
        SELECT @InTime=InTimeDT,@OutTime=OutTimeDT,@InFact=InFact,@OutFact=OutFact
          FROM #ClassSect WHERE SectNo=@SectNo
        IF (@InTime IS NULL OR @OutTime IS NULL) AND @Mode<>3 BREAK
        IF @InFact IS NOT NULL
        BEGIN
            IF @OutFact IS NULL SET @BCardForgetFlag=1
            IF @SectNo=1 SET @S1In=CONVERT(VARCHAR(16),@InFact,120)
            ELSE IF @SectNo=2 SET @S2In=CONVERT(VARCHAR(16),@InFact,120)
            ELSE IF @SectNo=3 SET @S3In=CONVERT(VARCHAR(16),@InFact,120)
            ELSE IF @SectNo=4 SET @S4In=CONVERT(VARCHAR(16),@InFact,120)
            ELSE IF @SectNo=5 SET @S5In=CONVERT(VARCHAR(16),@InFact,120)
        END
        IF @OutFact IS NOT NULL
        BEGIN
            IF @InFact IS NULL SET @BCardForgetFlag=1
            IF @SectNo=1 SET @S1Out=CONVERT(VARCHAR(16),@OutFact,120)
            ELSE IF @SectNo=2 SET @S2Out=CONVERT(VARCHAR(16),@OutFact,120)
            ELSE IF @SectNo=3 SET @S3Out=CONVERT(VARCHAR(16),@OutFact,120)
            ELSE IF @SectNo=4 SET @S4Out=CONVERT(VARCHAR(16),@OutFact,120)
            ELSE IF @SectNo=5 SET @S5Out=CONVERT(VARCHAR(16),@OutFact,120)
        END
        SET @SectNo=@SectNo+1
    END

    /* 刷卡数据 Brush_Data (fnGetBrushData) */
    IF @Mode<>4
        SELECT @BrushData=LEFT(ISNULL(STUFF((
            SELECT ','+CASE WHEN pc.Brush_Date>@CurDate
                            THEN dbo.fnKQ_ChangeTime(pc.Brush_Date,pc.Brush_Time,@CurDate)
                            ELSE pc.Brush_Time END
              FROM #PersonCard pc
             WHERE pc.Brush_Date>=@CurDate
               AND EXISTS(SELECT 1 FROM #ClassSect cs
                           WHERE cs.InTimeDT IS NOT NULL AND cs.OutTimeDT IS NOT NULL
                             AND pc.Brush_TimeDT>=cs.InTimeBgn AND pc.Brush_TimeDT<=cs.OutTimeEnd)
             ORDER BY pc.Card_Index
             FOR XML PATH(''),TYPE).value('.','VARCHAR(MAX)'),1,1,''),''),255)
    ELSE SET @BrushData=''

    /* 漏刷卡 (旷工>1小时 且 启用漏刷卡 且 有单边刷卡) */
    IF @BCardForget=1 AND @AbsentWork>1 AND @BCardForgetFlag=1 SET @Reason=@TxtForget

    SET @OfWeek=SUBSTRING(N'一二三四五六日',(DATEPART(weekday,@CurDateDT)+@@DATEFIRST-2)%7+1,1)

    /*INSERT INTO #Daily (Person_ID,Worktime_Date,OfClass,OfClassX,OfClassY,OfClassZ,*/
	INSERT INTO #Daily (Person_ID, Worktime_Date, OfWeek, Class_No, Class_Data, Brush_Data,
        Sect1_InFact,Sect1_OutFact,Sect2_InFact,Sect2_OutFact,Sect3_InFact,Sect3_OutFact,
        Sect4_InFact,Sect4_OutFact,Sect5_InFact,Sect5_OutFact,
        Must_Work,Fact_Work,Absent_Work,Reason,Leave_Hour,Leave_Data,Leave_Name,
        Overtime_Hour,Overtime_Class,Late_Minute,Late_Data,Early_Minute,Early_Data)
    SELECT c.Person_ID, @CurDate, @OfWeek, c.ClassNo, c.ClassData, @BrushData,
           ISNULL(@S1In,''),ISNULL(@S1Out,''),ISNULL(@S2In,''),ISNULL(@S2Out,''),
           ISNULL(@S3In,''),ISNULL(@S3Out,''),ISNULL(@S4In,''),ISNULL(@S4Out,''),
           ISNULL(@S5In,''),ISNULL(@S5Out,''),
           c.MustWork, c.FactWork, @AbsentWork, @Reason, c.LeaveHour, c.LeaveData, c.LeaveName,
           @OvertimeHour, @OvertimeClass, c.LateMinute, c.LateData, c.EarlyMinute, c.EarlyData
      FROM #Ctx c WHERE c.Id=1
END
GO

IF OBJECT_ID('dbo.KQ_pd_CalcOneDayFull') IS NOT NULL DROP PROC dbo.KQ_pd_CalcOneDayFull
GO
CREATE PROC dbo.KQ_pd_CalcOneDayFull @ClassNo VARCHAR(20), @CurDate VARCHAR(10), @CurDateDT DATETIME
AS                     -- 对应 fnCalcOneDayData: 设置班次→初始化→匹配刷卡→计算→写入
BEGIN
    DECLARE @r INT
    EXEC @r = dbo.KQ_pd_SetClassInfo @ClassNo
    IF @r = 0 RETURN 0                       -- 没有找到班次, 跳过此天
    EXEC dbo.KQ_pd_InitDailyInfo
    EXEC dbo.KQ_pd_MatchClassSect @CurDateDT
    EXEC dbo.KQ_pd_CalcOneDay @CurDate, @CurDateDT
    RETURN 1
END
GO

IF OBJECT_ID('dbo.KQ_pd_CalcDaily') IS NOT NULL DROP PROC dbo.KQ_pd_CalcDaily
GO
/*
-- 是否启用审批流 @bAudit=1 时, 异常单/增补刷卡需 AD_Auditing.ADState='11111111'
EXEC dbo.KQ_pd_CalcDaily '2024-11-01', '2024-11-18', '1001',
-- 查看结果
SELECT * FROM KQ_Daily WHERE Worktime_Date BETWEEN '2024-11-01' AND '2024-11-18'
*/
CREATE PROC dbo.KQ_pd_CalcDaily
    @DateBgn     VARCHAR(10),          -- 开始日期 yyyy-mm-dd
    @DateEnd     VARCHAR(10),          -- 结束日期
    @PersonList varchar(8000) = '',
    @bAudit      BIT = 0,              -- 是否启用审批流程(对应 Is_Auditing 参数)
    @BCardForget BIT = 0,              -- 是否启用漏刷卡功能(原程序中该开关被注释, 恒为False)
    @TableSuffix VARCHAR(20) = ''      -- 账套后缀: ''=活动表, 'History'=历史表 (对应 fnGetTableName)
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @DateBgnDT DATETIME, @DateEndDT DATETIME
    SET @DateBgnDT=CAST(@DateBgn AS DATETIME) SET @DateEndDT=CAST(@DateEnd AS DATETIME)

    DECLARE @Person_ID INT, @Person_Name NVARCHAR(30), @Class_No VARCHAR(20),
            @WorktimeMode INT, @OTMode INT, @RestMode INT, @RestDays VARCHAR(60),
            @OnWork VARCHAR(10), @OffWork VARCHAR(10), @IsOTWT BIT, @IsHoliWT BIT,
            @CurDate VARCHAR(10), @CurDateDT DATETIME, @DayClassNo VARCHAR(20),
            @CurClassNo VARCHAR(20), @ClassType CHAR(1), @PriorClassNo VARCHAR(20),
            @SubNo VARCHAR(20), @Score DECIMAL(12,2), @RecIdx INT, @BestNo VARCHAR(20), @BestRec INT,
            @MaxIdx INT, @cur INT, @found INT, @r INT,
            @DailyTbl SYSNAME, @sql NVARCHAR(4000)

    /*========== 0.人员范围: 若调用方未建 #Person 则统计全部人员 (fnPreparePerson) ==========*/
    IF OBJECT_ID('tempdb..#Person') IS NULL
    BEGIN
        CREATE TABLE #Person (Person_ID INT PRIMARY KEY)
        /*INSERT INTO #Person SELECT Person_ID FROM KQ_Person*/
    END

	IF NULLIF(@PersonList,'') IS NOT NULL
    BEGIN
        DECLARE @tmp varchar(8000), @item varchar(20), @p int
        SET @tmp = REPLACE(@PersonList,' ','') + ','
        SET @p = CHARINDEX(',',@tmp)
        WHILE @p > 1
        BEGIN
            SET @item = LEFT(@tmp,@p-1)
            IF ISNUMERIC(@item)=1
                INSERT #Person SELECT CAST(@item AS int)
                   WHERE NOT EXISTS(SELECT 1 FROM #Person WHERE Person_ID=CAST(@item AS int))
            SET @tmp = SUBSTRING(@tmp,@p+1,8000)
            SET @p = CHARINDEX(',',@tmp)
        END
    END
    ELSE
        INSERT #Person SELECT Person_ID FROM KQ_Person

    /*========== 1.公共临时表 ==========*/
    IF OBJECT_ID('tempdb..#Ctx') IS NOT NULL DROP TABLE #Ctx
    CREATE TABLE #Ctx (
        Id INT PRIMARY KEY, Person_ID INT, Person_Name NVARCHAR(30),
        Worktime_Mode INT, Overtime_Mode INT, Rest_Mode INT, Rest_Days VARCHAR(60),
        OnWork_Date VARCHAR(10), OffWork_Date VARCHAR(10),
        IsOverTimeAsWorkTime BIT, IsHolidayAsWorkTime BIT,
        RecIndex INT, CurDateDT DATETIME, BCardForget INT,
        ClassNo VARCHAR(20), ClassName NVARCHAR(50), ClassData VARCHAR(100),
        RuleMode INT, RuleCycle INT, RuleStart DATETIME,
        Late1S INT, Late1E INT, Late2E INT, Late3E INT,
        Early1S INT, Early1E INT, Early2E INT, Early3E INT,
        MinToHour INT, OneHour INT, HalfHour INT, Decimals INT,
        LeaveDatum INT, OvertimeDatum INT, OvertimeBig INT,
        IsDeducte INT, IsDeductLE INT, OutAddMinute INT,
        TxtAbsent NVARCHAR(10), TxtLeave NVARCHAR(10), TxtLate NVARCHAR(10), TxtEarly NVARCHAR(10),
        TxtFillClass NVARCHAR(10), TxtAdjustRest NVARCHAR(10), TxtFillAttend NVARCHAR(10), TxtForget NVARCHAR(10),
        MustWork DECIMAL(12,2), FactWork DECIMAL(12,2),
        OvertimeHour DECIMAL(12,2), OvertimeClass VARCHAR(2),
        LateMinute INT, LateData VARCHAR(200), EarlyMinute INT, EarlyData VARCHAR(200),
        Reason NVARCHAR(200), AbsentWork DECIMAL(12,2),
        LeaveHour DECIMAL(12,2), LeaveData VARCHAR(200), LeaveName NVARCHAR(200),
        OverAbsent DECIMAL(12,2),
        Sect1Must DECIMAL(12,2), Sect1Fact DECIMAL(12,2),
        Sect1LateMinute INT, Sect1EarlyMinute INT,
        Sect1LateData VARCHAR(200), Sect1EarlyData VARCHAR(200))
    INSERT INTO #Ctx (Id, RecIndex, BCardForget, OutAddMinute,
        TxtAbsent, TxtLeave, TxtLate, TxtEarly, TxtFillClass, TxtAdjustRest, TxtFillAttend, TxtForget)
    VALUES (1, 0, @BCardForget, 30,
            N'旷工', N'请假', N'迟到', N'早退', N'调休补班', N'调休', N'补出勤', N'漏刷卡')

    /* 装载考勤参数 (fnInitAttendParam) */
    UPDATE c SET
        c.Late1S=ISNULL(p.late1s,0),      c.Late1E=ISNULL(p.late1e,0),
        c.Late2E=ISNULL(p.late2e,0),      c.Late3E=ISNULL(p.late3e,0),
        c.Early1S=ISNULL(p.early1s,0),    c.Early1E=ISNULL(p.early1e,0),
        c.Early2E=ISNULL(p.early2e,0),    c.Early3E=ISNULL(p.early3e,0),
        c.OneHour=ISNULL(p.onehour,0),    c.HalfHour=ISNULL(p.halfhour,0),
        c.MinToHour=ISNULL(p.mintohour,0),c.Decimals=ISNULL(p.decimals,0),
        c.LeaveDatum=ISNULL(p.leave_datum,0),   c.OvertimeDatum=ISNULL(p.overtime_datum,0),
        c.OvertimeBig=ISNULL(p.overtimebig,0),  c.IsDeducte=ISNULL(p.is_deducte,0),
        c.IsDeductLE=ISNULL(p.isdeductle,0),    c.OutAddMinute=ISNULL(p.outaddminute,30)
    FROM #Ctx c CROSS JOIN (
        SELECT
          MAX(CASE WHEN Param_Code='late1s'           THEN CAST(Param_Value AS INT) END) late1s,
          MAX(CASE WHEN Param_Code='late1e'           THEN CAST(Param_Value AS INT) END) late1e,
          MAX(CASE WHEN Param_Code='late2e'           THEN CAST(Param_Value AS INT) END) late2e,
          MAX(CASE WHEN Param_Code='late3e'           THEN CAST(Param_Value AS INT) END) late3e,
          MAX(CASE WHEN Param_Code='early1s'          THEN CAST(Param_Value AS INT) END) early1s,
          MAX(CASE WHEN Param_Code='early1e'          THEN CAST(Param_Value AS INT) END) early1e,
          MAX(CASE WHEN Param_Code='early2e'          THEN CAST(Param_Value AS INT) END) early2e,
          MAX(CASE WHEN Param_Code='early3e'          THEN CAST(Param_Value AS INT) END) early3e,
          MAX(CASE WHEN Param_Code='onehour'          THEN CAST(Param_Value AS INT) END) onehour,
          MAX(CASE WHEN Param_Code='halfhour'         THEN CAST(Param_Value AS INT) END) halfhour,
          MAX(CASE WHEN Param_Code='mintohour'        THEN CAST(Param_Value AS INT) END) mintohour,
          MAX(CASE WHEN Param_Code='decimal'          THEN CAST(Param_Value AS INT) END) decimals,
          MAX(CASE WHEN Param_Code='leave_standard'   THEN CAST(Param_Value AS INT) END) leave_datum,
          MAX(CASE WHEN Param_Code='overtime_standard'THEN CAST(Param_Value AS INT) END) overtime_datum,
          MAX(CASE WHEN Param_Code='overtimebig'      THEN CAST(Param_Value AS INT) END) overtimebig,
          MAX(CASE WHEN Param_Code='is_deducte'       THEN CAST(Param_Value AS INT) END) is_deducte,
          MAX(CASE WHEN Param_Code='isdeductle'       THEN CAST(Param_Value AS INT) END) isdeductle,
          MAX(CASE WHEN Param_Code='voutaddminute'    THEN CAST(Param_Value AS INT) END) outaddminute
        FROM KQ_SysParam) p
     WHERE c.Id=1

    IF OBJECT_ID('tempdb..#ClassSect') IS NOT NULL DROP TABLE #ClassSect
    CREATE TABLE #ClassSect (
        SectNo INT PRIMARY KEY, TimeIn VARCHAR(5), TimeOut VARCHAR(5),
        IncBgnIn INT, IncEndIn INT, IncBgnOut INT, IncEndOut INT,
        InCard INT, OutCard INT, Deduct INT, Overtime BIT,
        InTimeDT DATETIME NULL, OutTimeDT DATETIME NULL,
        InTimeBgn DATETIME NULL, InTimeEnd DATETIME NULL,
        OutTimeBgn DATETIME NULL, OutTimeEnd DATETIME NULL,
        InFact DATETIME NULL, OutFact DATETIME NULL)

    IF OBJECT_ID('tempdb..#PersonCard') IS NOT NULL DROP TABLE #PersonCard
    CREATE TABLE #PersonCard (Card_Index INT IDENTITY PRIMARY KEY,
        Brush_Date VARCHAR(10), Brush_Time VARCHAR(8), Data_Flag INT, Brush_TimeDT DATETIME)
    IF OBJECT_ID('tempdb..#AdjClass') IS NOT NULL DROP TABLE #AdjClass
    CREATE TABLE #AdjClass (Adjust_Date VARCHAR(10), Class_No VARCHAR(20))
    IF OBJECT_ID('tempdb..#Leave') IS NOT NULL DROP TABLE #Leave
    CREATE TABLE #Leave (Leave_Date VARCHAR(10), Kind_No VARCHAR(20), Kind_Name NVARCHAR(50),
                         Leave_Hour DECIMAL(12,2), Is_Deduct BIT, IsOutLeave BIT)
    IF OBJECT_ID('tempdb..#AdjRest') IS NOT NULL DROP TABLE #AdjRest
    CREATE TABLE #AdjRest (Adjust_Date VARCHAR(10), Adjust_Class INT, Adjust_Hour DECIMAL(12,2))
    IF OBJECT_ID('tempdb..#OverTime') IS NOT NULL DROP TABLE #OverTime
    CREATE TABLE #OverTime (Over_Date VARCHAR(10), Over_Hour DECIMAL(12,2))
    IF OBJECT_ID('tempdb..#FillAttend') IS NOT NULL DROP TABLE #FillAttend
    CREATE TABLE #FillAttend (Fill_Date VARCHAR(10), Fill_Class INT, Fill_Hour DECIMAL(12,2))
    IF OBJECT_ID('tempdb..#RuleSub') IS NOT NULL DROP TABLE #RuleSub
    CREATE TABLE #RuleSub (Seq INT IDENTITY PRIMARY KEY, Shift_ClassNo VARCHAR(20),
                           Shift_Days INT, RangeBgn INT, RangeEnd INT)
    IF OBJECT_ID('tempdb..#AgileScore') IS NOT NULL DROP TABLE #AgileScore
    CREATE TABLE #AgileScore (ClassNo VARCHAR(20), Score DECIMAL(12,2), RecIndex INT)
    IF OBJECT_ID('tempdb..#AgileFacts') IS NOT NULL DROP TABLE #AgileFacts
    CREATE TABLE #AgileFacts (ClassNo VARCHAR(20), SectNo INT, InFact DATETIME, OutFact DATETIME)

    /*========== 2.收集刷卡数据 (fnCollectBrushCard) ==========*/
    IF OBJECT_ID('tempdb..#BrushCard') IS NOT NULL DROP TABLE #BrushCard
    CREATE TABLE #BrushCard (Person_ID INT, Brush_Date VARCHAR(10), Brush_Time VARCHAR(8), Data_Flag INT)
    INSERT INTO #BrushCard
    SELECT DISTINCT Person_ID, Brush_Date, Brush_Time, Data_Flag
      FROM KQ_Download
     WHERE ISNULL(Data_Flag,0)<20
       AND Brush_Date>=CONVERT(VARCHAR(10),DATEADD(day,-1,@DateBgnDT),120)
       AND Brush_Date<=CONVERT(VARCHAR(10),DATEADD(day, 1,@DateEndDT),120)
       AND Person_ID IN (SELECT Person_ID FROM #Person)
    INSERT INTO #BrushCard
    SELECT DISTINCT s.Person_ID, s.Supply_Date, s.Supply_Time, s.Supply_Class    -- 增补刷卡
      FROM KQ_SupplyTime s
     WHERE s.Supply_Date>=CONVERT(VARCHAR(10),DATEADD(day,-1,@DateBgnDT),120)
       AND s.Supply_Date<=CONVERT(VARCHAR(10),DATEADD(day, 1,@DateEndDT),120)
       AND s.Person_ID IN (SELECT Person_ID FROM #Person)
       /*AND (@bAudit=0 OR EXISTS(SELECT 1 FROM AD_Auditing a WHERE a.BillIDKEY=s.ID_Key AND a.Bill_TableName='KQ_SupplyTime' AND a.ADState='11111111'))*/

    /*========== 3.日报结果临时表 (#Daily) ==========*/
    IF OBJECT_ID('tempdb..#Daily') IS NOT NULL DROP TABLE #Daily
    CREATE TABLE #Daily (
        Person_ID INT, Worktime_Date VARCHAR(10), OfWeek NVARCHAR(4),
        Class_No VARCHAR(20), Class_Data VARCHAR(100), Brush_Data VARCHAR(255),
        Sect1_InFact VARCHAR(16), Sect1_OutFact VARCHAR(16),
        Sect2_InFact VARCHAR(16), Sect2_OutFact VARCHAR(16),
        Sect3_InFact VARCHAR(16), Sect3_OutFact VARCHAR(16),
        Sect4_InFact VARCHAR(16), Sect4_OutFact VARCHAR(16),
        Sect5_InFact VARCHAR(16), Sect5_OutFact VARCHAR(16),
        Must_Work DECIMAL(12,2), Fact_Work DECIMAL(12,2), Absent_Work DECIMAL(12,2),
        Reason NVARCHAR(60), Leave_Hour DECIMAL(12,2), Leave_Data VARCHAR(100), Leave_Name NVARCHAR(100),
        Overtime_Hour DECIMAL(12,2), Overtime_Class VARCHAR(2),
        Late_Minute INT, Late_Data VARCHAR(100), Early_Minute INT, Early_Data VARCHAR(100))

    /*========== 4.逐人计算 (fnCycleDealPerson) ==========*/
    DECLARE cur_Person CURSOR LOCAL FAST_FORWARD FOR
        SELECT A.Person_ID, ISNULL(B.Person_Name,N''), A.Class_No,
               ISNULL(A.Worktime_Mode,1), ISNULL(A.Overtime_Mode,0),
               ISNULL(A.Rest_Mode,0), ISNULL(A.Rest_Days,''),
               CASE WHEN ISNULL(B.OnWork_Date,'')=''  THEN '' ELSE CONVERT(VARCHAR(10),CAST(B.OnWork_Date  AS DATETIME),120) END,
               CASE WHEN ISNULL(B.OffWork_Date,'')='' THEN '' ELSE CONVERT(VARCHAR(10),CAST(B.OffWork_Date AS DATETIME),120) END,
               ISNULL(A.IsOverTimeAsWorkTime,0), ISNULL(A.IsHolidayAsWorkTime,0)
          FROM KQ_Person A LEFT JOIN ST_Person B ON B.Person_ID=A.Person_ID
         WHERE A.Person_ID IN (SELECT Person_ID FROM #Person)
         ORDER BY A.Class_No, A.Person_ID
    OPEN cur_Person
    FETCH NEXT FROM cur_Person INTO @Person_ID,@Person_Name,@Class_No,
            @WorktimeMode,@OTMode,@RestMode,@RestDays,@OnWork,@OffWork,@IsOTWT,@IsHoliWT
    WHILE @@FETCH_STATUS=0
    BEGIN
        IF LTRIM(RTRIM(ISNULL(@Class_No,'')))<>''                    -- 班次为空跳过
        BEGIN
            IF @WorktimeMode NOT IN (1,2,3,4) SET @WorktimeMode=1    -- 未知方式按"按班次刷卡"

            /* fnInitAttendInfo */
            UPDATE #Ctx SET Person_ID=@Person_ID, Person_Name=@Person_Name,
                   Worktime_Mode=@WorktimeMode, Overtime_Mode=@OTMode,
                   Rest_Mode=@RestMode, Rest_Days=@RestDays,
                   OnWork_Date=@OnWork, OffWork_Date=@OffWork,
                   IsOverTimeAsWorkTime=@IsOTWT, IsHolidayAsWorkTime=@IsHoliWT,
                   RecIndex=0, CurDateDT=NULL, ClassNo='', ClassName='', ClassData='',
                   RuleMode=0, RuleCycle=0, RuleStart=NULL
             WHERE Id=1

            /* 装载个人原始刷卡记录 (fnLoadPersonData) */
            TRUNCATE TABLE #PersonCard
            INSERT INTO #PersonCard (Brush_Date, Brush_Time, Data_Flag, Brush_TimeDT)
            SELECT Brush_Date, Brush_Time, Data_Flag, CAST(Brush_Date+' '+Brush_Time AS DATETIME)
              FROM #BrushCard WHERE Person_ID=@Person_ID
             ORDER BY Brush_Date, Brush_Time

            /* 装载个人异常数据 (fnLoadExceptionData, 含审批流过滤) */
            TRUNCATE TABLE #AdjClass
            INSERT INTO #AdjClass SELECT Adjust_Date, Class_No FROM KQ_AdjustClass a
             WHERE a.Person_ID=@Person_ID AND a.Adjust_Date BETWEEN @DateBgn AND @DateEnd
               /* AND (@bAudit=0 OR EXISTS(SELECT 1 FROM AD_Auditing x  WHERE x.BillIDKEY=a.ID_Key AND x.Bill_TableName='KQ_AdjustClass' AND x.ADState='11111111'))*/
            TRUNCATE TABLE #Leave
            INSERT INTO #Leave
            SELECT l.Leave_Date, k.Kind_No, k.Kind_Name, l.Leave_Hour, ISNULL(k.Is_Deduct,0), ISNULL(l.IsOutLeave,0)
              FROM KQ_Leave l LEFT JOIN KQ_LeaveKind k ON k.Kind_No=l.Kind_No
             WHERE l.Person_ID=@Person_ID AND l.Leave_Date BETWEEN @DateBgn AND @DateEnd
              /*  AND (@bAudit=0 OR EXISTS(SELECT 1 FROM AD_Auditing x
                     WHERE x.BillIDKEY=l.ID_Key AND x.Bill_TableName='KQ_Leave' AND x.ADState='11111111'))*/
            TRUNCATE TABLE #AdjRest
            INSERT INTO #AdjRest SELECT Adjust_Date, Adjust_Class, Adjust_Hour FROM KQ_AdjustRest a
             WHERE a.Person_ID=@Person_ID AND a.Adjust_Date BETWEEN @DateBgn AND @DateEnd
              /*  AND (@bAudit=0 OR EXISTS(SELECT 1 FROM AD_Auditing x
                     WHERE x.BillIDKEY=a.ID_Key AND x.Bill_TableName='KQ_AdjustRest' AND x.ADState='11111111'))*/
            TRUNCATE TABLE #OverTime
            INSERT INTO #OverTime SELECT Over_Date, Over_Hour FROM KQ_OverTime a
             WHERE a.Person_ID=@Person_ID AND a.Over_Date BETWEEN @DateBgn AND @DateEnd
              /*  AND (@bAudit=0 OR EXISTS(SELECT 1 FROM AD_Auditing x
                     WHERE x.BillIDKEY=a.ID_Key AND x.Bill_TableName='KQ_OverTime' AND x.ADState='11111111'))*/
            TRUNCATE TABLE #FillAttend
            INSERT INTO #FillAttend SELECT Fill_Date, Fill_Class, Fill_Hour FROM KQ_FillAttend a
             WHERE a.Person_ID=@Person_ID AND a.Fill_Date BETWEEN @DateBgn AND @DateEnd
              /*  AND (@bAudit=0 OR EXISTS(SELECT 1 FROM AD_Auditing x
                     WHERE x.BillIDKEY=a.ID_Key AND x.Bill_TableName='KQ_FillAttend' AND x.ADState='11111111'))*/

            /* 班次类型: A正常 B规律 C灵活 其他=自定义 */
            SET @ClassType=LEFT(@Class_No,1)
            SET @CurClassNo='' SET @PriorClassNo=''
            IF @ClassType='C' AND NOT EXISTS(SELECT 1 FROM KQ_AgileClassSub WHERE Class_No=@Class_No) SET @ClassType='X'
            IF @ClassType NOT IN ('A','B','C')
               AND NOT EXISTS(SELECT 1 FROM KQ_DefineClassSub WHERE Class_No=@Class_No) SET @ClassType='X'

            SET @CurDateDT=@DateBgnDT
            WHILE @CurDateDT<=@DateEndDT AND @ClassType<>'X'
            BEGIN
                SET @CurDate=CONVERT(VARCHAR(10),@CurDateDT,120)
                UPDATE #Ctx SET CurDateDT=@CurDateDT WHERE Id=1

                /* 未上岗/已离职跳过 (fnDateWorking) */
                IF (@OnWork<>'' AND @CurDate<@OnWork) OR (@OffWork<>'' AND @CurDate>=@OffWork)
                BEGIN SET @CurDateDT=@CurDateDT+1 CONTINUE END

                /*---------- 灵活班次 ----------*/
                IF @ClassType='C' AND EXISTS(SELECT 1 FROM #AdjClass WHERE Adjust_Date=@CurDate)
                BEGIN                                   -- 当天有调班按普通方式
                    SELECT @DayClassNo=Class_No FROM #AdjClass WHERE Adjust_Date=@CurDate
                    SET @PriorClassNo=@DayClassNo
                    EXEC dbo.KQ_pd_CalcOneDayFull @DayClassNo, @CurDate, @CurDateDT
                END
                ELSE IF @ClassType='C'
                BEGIN
                    /*---- 各子班次评分取最优 (fnCalcAgileClass) ----*/
                    TRUNCATE TABLE #AgileScore TRUNCATE TABLE #AgileFacts
                    EXEC dbo.KQ_pd_InitDailyInfo
                    DECLARE cur_Agile CURSOR LOCAL FAST_FORWARD FOR
                        SELECT Shift_ClassNo FROM KQ_AgileClassSub WHERE Class_No=@Class_No ORDER BY Shift_ClassNo
                    OPEN cur_Agile FETCH NEXT FROM cur_Agile INTO @SubNo
                    WHILE @@FETCH_STATUS=0
                    BEGIN
                        EXEC dbo.KQ_pd_SetClassInfo @SubNo
                        /* fnLocateFirstBrushCard: 定位当天第一笔刷卡 */
                        SELECT @MaxIdx=ISNULL(MAX(Card_Index),0) FROM #PersonCard
                        IF @MaxIdx>0
                        BEGIN
                            SELECT @cur=RecIndex+1 FROM #Ctx WHERE Id=1
                            IF @cur>@MaxIdx SET @cur=@MaxIdx
                            SET @found=@cur-1
                            WHILE @cur>=1
                            BEGIN
                                IF (SELECT Brush_Date FROM #PersonCard WHERE Card_Index=@cur)<@CurDate OR @cur=1
                                BEGIN SET @found=@cur-1 BREAK END
                                SET @cur=@cur-1
                            END
                            UPDATE #Ctx SET RecIndex=@found WHERE Id=1
                        END
                        EXEC dbo.KQ_pd_MatchClassSect @CurDateDT
                        INSERT INTO #AgileFacts SELECT @SubNo, SectNo, InFact, OutFact FROM #ClassSect
                        SELECT @RecIdx=RecIndex FROM #Ctx WHERE Id=1
                        EXEC dbo.KQ_pd_CalcClassScore @SubNo, @PriorClassNo, @Score OUTPUT
                        INSERT INTO #AgileScore VALUES(@SubNo, @Score, @RecIdx)
                        FETCH NEXT FROM cur_Agile INTO @SubNo
                    END
                    CLOSE cur_Agile DEALLOCATE cur_Agile

                    SELECT TOP 1 @BestNo=ClassNo, @BestRec=RecIndex FROM #AgileScore ORDER BY Score DESC
                    IF @BestNo IS NOT NULL
                    BEGIN
                        EXEC dbo.KQ_pd_SetClassInfo @BestNo              -- 恢复最佳班次
                        UPDATE cs SET cs.InFact=f.InFact, cs.OutFact=f.OutFact
                          FROM #ClassSect cs JOIN #AgileFacts f
                            ON f.ClassNo=@BestNo AND f.SectNo=cs.SectNo
                        UPDATE #Ctx SET RecIndex=@BestRec WHERE Id=1
                        EXEC dbo.KQ_pd_CalcOneDay @CurDate, @CurDateDT
                        SET @PriorClassNo=@BestNo
                    END
                END
                /*---------- 正常 / 规律 / 自定义班次 ----------*/
                ELSE
                BEGIN
                    IF EXISTS(SELECT 1 FROM #AdjClass WHERE Adjust_Date=@CurDate)
                    BEGIN
                        SELECT @DayClassNo=Class_No FROM #AdjClass WHERE Adjust_Date=@CurDate
                        IF LEFT(@DayClassNo,1)='B'                       -- 调整为规律班次
                        BEGIN
                            IF ISNULL(@CurClassNo,'')<>@DayClassNo
                            BEGIN
                                EXEC @r=dbo.KQ_pd_SetRuleClassInfo @DayClassNo
                                IF @r=0 BEGIN SET @CurDateDT=@CurDateDT+1 CONTINUE END
                                SET @CurClassNo=@DayClassNo
                            END
                            EXEC dbo.KQ_pd_GetRuleClassNo @DayClassNo OUTPUT
                        END
                        EXEC dbo.KQ_pd_CalcOneDayFull @DayClassNo, @CurDate, @CurDateDT
                    END
                    ELSE
                    BEGIN
                        IF @ClassType='A' SET @DayClassNo=@Class_No      -- 正常班次
                        ELSE IF @ClassType='B'                           -- 规律班次
                        BEGIN
                            IF ISNULL(@CurClassNo,'')<>@Class_No
                            BEGIN
                                EXEC @r=dbo.KQ_pd_SetRuleClassInfo @Class_No
                                IF @r=0 BEGIN SET @CurDateDT=@CurDateDT+1 CONTINUE END
                                SET @CurClassNo=@Class_No
                            END
                            EXEC dbo.KQ_pd_GetRuleClassNo @DayClassNo OUTPUT
                        END
                        ELSE                                             -- 自定义班次
                        BEGIN
                            SELECT @DayClassNo=ISNULL(LTRIM(RTRIM(Day_ClassNo)),'')
                              FROM KQ_DefineClassSub
                             WHERE Class_No=@Class_No AND Worktime_Date=@CurDate
                            IF @DayClassNo='' BEGIN SET @CurDateDT=@CurDateDT+1 CONTINUE END
                        END
                        EXEC dbo.KQ_pd_CalcOneDayFull @DayClassNo, @CurDate, @CurDateDT
                    END
                END
                SET @CurDateDT=@CurDateDT+1
            END
        END
        FETCH NEXT FROM cur_Person INTO @Person_ID,@Person_Name,@Class_No,
                @WorktimeMode,@OTMode,@RestMode,@RestDays,@OnWork,@OffWork,@IsOTWT,@IsHoliWT
    END
    CLOSE cur_Person DEALLOCATE cur_Person

    /*========== 5.保存结果 (fnSaveCalcResult, 支持历史表后缀) ==========*/
    SET @DailyTbl=QUOTENAME('KQ_Daily'+@TableSuffix)
    BEGIN TRY
        BEGIN TRAN
        SET @sql=N'DELETE FROM '+@DailyTbl+
                  N' WHERE Worktime_Date>=@d1 AND Worktime_Date<=@d2'+
                  N' AND Person_ID IN (SELECT Person_ID FROM #Person)'
        EXEC sp_executesql @sql, N'@d1 VARCHAR(10),@d2 VARCHAR(10)', @DateBgn, @DateEnd

        SET @sql=N'INSERT INTO '+@DailyTbl+N'(
 Person_ID,Worktime_Date,OfWeek,Class_No,Class_Data,Brush_Data,
 Sect1_InFact,Sect1_OutFact,Sect2_InFact,Sect2_OutFact,Sect3_InFact,Sect3_OutFact,
 Sect4_InFact,Sect4_OutFact,Sect5_InFact,Sect5_OutFact,
 Must_Work,Fact_Work,Absent_Work,Reason,Leave_Hour,Leave_Data,Leave_Name,
 Overtime_Hour,Overtime_Class,Late_Minute,Late_Data,Early_Minute,Early_Data)
 SELECT Person_ID,Worktime_Date,OfWeek,Class_No,Class_Data,Brush_Data,
 Sect1_InFact,Sect1_OutFact,Sect2_InFact,Sect2_OutFact,Sect3_InFact,Sect3_OutFact,
 Sect4_InFact,Sect4_OutFact,Sect5_InFact,Sect5_OutFact,
 Must_Work,Fact_Work,Absent_Work,Reason,Leave_Hour,Leave_Data,Leave_Name,
 Overtime_Hour,Overtime_Class,Late_Minute,Late_Data,Early_Minute,Early_Data
 FROM #Daily'
        EXEC(@sql)
        COMMIT
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT>0 ROLLBACK
        DECLARE @msg NVARCHAR(2000) SET @msg=ERROR_MESSAGE()
        RAISERROR(@msg,16,1)
    END CATCH
END
GO

/*===========================================================
  微信充值幂等与查询性能（V2/V3 共用）
  ST_SupplyFund.Out_Trade_No 用于「同一商户单号只入账一次」，
  唯一索引可从根本上防止异步通知与前端查询并发时重复加钱。
===========================================================*/
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ST_SupplyFund_Out_Trade_No' AND object_id = OBJECT_ID('ST_SupplyFund'))
   AND NOT EXISTS (SELECT Out_Trade_No FROM ST_SupplyFund WHERE Out_Trade_No IS NOT NULL GROUP BY Out_Trade_No HAVING COUNT(1) > 1)
BEGIN
    CREATE UNIQUE INDEX UX_ST_SupplyFund_Out_Trade_No ON ST_SupplyFund(Out_Trade_No) WHERE Out_Trade_No IS NOT NULL
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WX_RechargeLog_Out_Trade_No' AND object_id = OBJECT_ID('WX_RechargeLog'))
BEGIN
    CREATE INDEX IX_WX_RechargeLog_Out_Trade_No ON WX_RechargeLog(Out_Trade_No)
END
GO


/*===========================================================
  人脸通 BS：系统管理-组织与权限
  1) 只新增 BS_ 前缀对象与可空扩展列，不修改/删除 CS 既有结构
  2) 脚本幂等，可重复执行
  3) BS 菜单写入 ST_MenuInfo（System_No='BS'，Menu_ID 9000-9999 段）
===========================================================*/

/*---------- 12.3 新增可空列 ----------*/
IF COL_LENGTH('dbo.ST_UserInfo', 'BS_Dept_ID') IS NULL
    ALTER TABLE dbo.ST_UserInfo ADD BS_Dept_ID INT NULL;
IF COL_LENGTH('dbo.ST_UserInfo', 'BS_Fail_Count') IS NULL
    ALTER TABLE dbo.ST_UserInfo ADD BS_Fail_Count SMALLINT NULL;
IF COL_LENGTH('dbo.ST_UserInfo', 'BS_Lock_Until') IS NULL
    ALTER TABLE dbo.ST_UserInfo ADD BS_Lock_Until DATETIME NULL;
IF COL_LENGTH('dbo.ST_UserInfo', 'BS_Pwd_Update_Time') IS NULL
    ALTER TABLE dbo.ST_UserInfo ADD BS_Pwd_Update_Time DATETIME NULL;
IF COL_LENGTH('dbo.ST_UserInfo', 'BS_Last_Login_IP') IS NULL
    ALTER TABLE dbo.ST_UserInfo ADD BS_Last_Login_IP VARCHAR(45) NULL;
IF COL_LENGTH('dbo.ST_Department', 'BS_Parent_ID') IS NULL
    ALTER TABLE dbo.ST_Department ADD BS_Parent_ID INT NULL;
IF COL_LENGTH('dbo.ST_Department', 'BS_Level') IS NULL
    ALTER TABLE dbo.ST_Department ADD BS_Level TINYINT NULL;
IF COL_LENGTH('dbo.ST_Department', 'BS_Sort_No') IS NULL
    ALTER TABLE dbo.ST_Department ADD BS_Sort_No INT NULL;
IF COL_LENGTH('dbo.ST_Department', 'BS_Dept_Type') IS NULL
    ALTER TABLE dbo.ST_Department ADD BS_Dept_Type VARCHAR(20) NULL;
GO

/*---------- 12.2 新增 BS_ 表 ----------*/
IF OBJECT_ID('dbo.BS_RightCodeMap', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BS_RightCodeMap (
        ID_KEY INT IDENTITY NOT NULL,
        Menu_ID INT NOT NULL,
        Right_ID CHAR(1) NOT NULL,
        Permission_Code VARCHAR(64) NOT NULL,
        Api_Route VARCHAR(200) NULL,
        Remark VARCHAR(200) NULL,
        CONSTRAINT PK_BS_RightCodeMap PRIMARY KEY CLUSTERED (ID_KEY),
        CONSTRAINT UQ_BS_RightCodeMap_Menu_Right UNIQUE (Menu_ID, Right_ID)
    )
END
GO

IF OBJECT_ID('dbo.BS_UserDataScope', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BS_UserDataScope (
        User_No VARCHAR(20) NOT NULL,
        Scope_Type TINYINT NOT NULL CONSTRAINT DF_BS_UserDataScope_Type DEFAULT (3),
        Owner_Dept_ID INT NULL,
        Summary VARCHAR(200) NULL,
        Update_Time DATETIME NOT NULL CONSTRAINT DF_BS_UserDataScope_Time DEFAULT (GETDATE()),
        Update_User VARCHAR(20) NULL,
        CONSTRAINT PK_BS_UserDataScope PRIMARY KEY CLUSTERED (User_No)
    )
END
GO

IF OBJECT_ID('dbo.BS_UserMOCRight', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BS_UserMOCRight (
        User_No VARCHAR(20) NOT NULL,
        MOC_No VARCHAR(6) NOT NULL,
        Right_Level TINYINT NOT NULL CONSTRAINT DF_BS_UserMOCRight_Level DEFAULT (1),
        Update_Time DATETIME NOT NULL CONSTRAINT DF_BS_UserMOCRight_Time DEFAULT (GETDATE()),
        CONSTRAINT PK_BS_UserMOCRight PRIMARY KEY CLUSTERED (User_No, MOC_No)
    )
END
GO

IF OBJECT_ID('dbo.BS_UserSubSysRight', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BS_UserSubSysRight (
        User_No VARCHAR(20) NOT NULL,
        System_No VARCHAR(10) NOT NULL,
        Is_Allow BIT NOT NULL CONSTRAINT DF_BS_UserSubSysRight_Allow DEFAULT (1),
        Update_Time DATETIME NOT NULL CONSTRAINT DF_BS_UserSubSysRight_Time DEFAULT (GETDATE()),
        CONSTRAINT PK_BS_UserSubSysRight PRIMARY KEY CLUSTERED (User_No, System_No)
    )
END
GO

IF OBJECT_ID('dbo.BS_OperLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BS_OperLog (
        ID_KEY BIGINT IDENTITY NOT NULL,
        User_No VARCHAR(20) NULL,
        User_Name VARCHAR(20) NULL,
        Module VARCHAR(30) NOT NULL,
        Action VARCHAR(30) NOT NULL,
        Target VARCHAR(100) NULL,
        Target_Key VARCHAR(50) NULL,
        Before_Value NVARCHAR(MAX) NULL,
        After_Value NVARCHAR(MAX) NULL,
        IP VARCHAR(45) NULL,
        User_Agent VARCHAR(200) NULL,
        Result TINYINT NOT NULL CONSTRAINT DF_BS_OperLog_Result DEFAULT (0),
        Message VARCHAR(500) NULL,
        Trace_ID VARCHAR(36) NULL,
        Opt_Time DATETIME NOT NULL CONSTRAINT DF_BS_OperLog_Time DEFAULT (GETDATE()),
        CONSTRAINT PK_BS_OperLog PRIMARY KEY CLUSTERED (ID_KEY)
    )
END
GO

IF OBJECT_ID('dbo.BS_LoginLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BS_LoginLog (
        ID_KEY BIGINT IDENTITY NOT NULL,
        User_No VARCHAR(20) NULL,
        Login_Type VARCHAR(20) NOT NULL,
        Result TINYINT NOT NULL CONSTRAINT DF_BS_LoginLog_Result DEFAULT (0),
        Fail_Reason VARCHAR(100) NULL,
        IP VARCHAR(45) NULL,
        User_Agent VARCHAR(200) NULL,
        Login_Time DATETIME NOT NULL CONSTRAINT DF_BS_LoginLog_Time DEFAULT (GETDATE()),
        Logout_Time DATETIME NULL,
        Session_ID VARCHAR(36) NULL,
        CONSTRAINT PK_BS_LoginLog PRIMARY KEY CLUSTERED (ID_KEY)
    )
END
GO

IF OBJECT_ID('dbo.BS_SysConfig', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BS_SysConfig (
        Config_ID INT IDENTITY NOT NULL,
        Config_Key VARCHAR(64) NOT NULL,
        Config_Value NVARCHAR(500) NULL,
        Remark VARCHAR(200) NULL,
        Update_Time DATETIME NOT NULL CONSTRAINT DF_BS_SysConfig_Time DEFAULT (GETDATE()),
        CONSTRAINT PK_BS_SysConfig PRIMARY KEY CLUSTERED (Config_ID),
        CONSTRAINT UQ_BS_SysConfig_Key UNIQUE (Config_Key)
    )
END
GO

IF OBJECT_ID('dbo.BS_PwdHistory', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BS_PwdHistory (
        ID_KEY BIGINT IDENTITY NOT NULL,
        User_No VARCHAR(20) NOT NULL,
        Pwd_Cipher VARCHAR(200) NOT NULL,
        Create_Time DATETIME NOT NULL CONSTRAINT DF_BS_PwdHistory_Time DEFAULT (GETDATE()),
        CONSTRAINT PK_BS_PwdHistory PRIMARY KEY CLUSTERED (ID_KEY)
    )
END
GO

IF OBJECT_ID('dbo.BS_AuthToken', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BS_AuthToken (
        Token_ID VARCHAR(36) NOT NULL,
        User_No VARCHAR(20) NOT NULL,
        Token_Hash VARCHAR(128) NOT NULL,
        Expire_Time DATETIME NOT NULL,
        Revoked BIT NOT NULL CONSTRAINT DF_BS_AuthToken_Revoked DEFAULT (0),
        Client_Info VARCHAR(200) NULL,
        Create_Time DATETIME NOT NULL CONSTRAINT DF_BS_AuthToken_Time DEFAULT (GETDATE()),
        CONSTRAINT PK_BS_AuthToken PRIMARY KEY CLUSTERED (Token_ID)
    )
END
GO

/*---------- 12.5 索引补充 ----------*/
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_UserInfo_Dept' AND object_id = OBJECT_ID('ST_UserInfo'))
    CREATE INDEX IX_BS_UserInfo_Dept ON dbo.ST_UserInfo (BS_Dept_ID) INCLUDE (Role_No, Is_Del);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_RightCodeMap_Code' AND object_id = OBJECT_ID('BS_RightCodeMap'))
    CREATE INDEX IX_BS_RightCodeMap_Code ON dbo.BS_RightCodeMap (Permission_Code);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_UserMOCRight_MOC' AND object_id = OBJECT_ID('BS_UserMOCRight'))
    CREATE INDEX IX_BS_UserMOCRight_MOC ON dbo.BS_UserMOCRight (MOC_No) INCLUDE (Right_Level);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_UserSubSysRight_System' AND object_id = OBJECT_ID('BS_UserSubSysRight'))
    CREATE INDEX IX_BS_UserSubSysRight_System ON dbo.BS_UserSubSysRight (System_No) INCLUDE (Is_Allow);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_OperLog_Time' AND object_id = OBJECT_ID('BS_OperLog'))
    CREATE INDEX IX_BS_OperLog_Time ON dbo.BS_OperLog (Opt_Time DESC, User_No);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_LoginLog_Time' AND object_id = OBJECT_ID('BS_LoginLog'))
    CREATE INDEX IX_BS_LoginLog_Time ON dbo.BS_LoginLog (Login_Time DESC, User_No);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_PwdHistory_User' AND object_id = OBJECT_ID('BS_PwdHistory'))
    CREATE INDEX IX_BS_PwdHistory_User ON dbo.BS_PwdHistory (User_No, Create_Time DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_AuthToken_User' AND object_id = OBJECT_ID('BS_AuthToken'))
    CREATE INDEX IX_BS_AuthToken_User ON dbo.BS_AuthToken (User_No, Revoked, Expire_Time);
GO

/*---------- 12.6 初始化：子系统、BS 菜单、权限位、权限码映射、版本 ----------*/
IF NOT EXISTS (SELECT 1 FROM dbo.ST_SubSystem WHERE System_No = 'BS')
    INSERT INTO dbo.ST_SubSystem (System_No, System_Name, System_ExeName, Index_No)
    VALUES ('BS', N'人脸通BS管理端', 'Web', 9);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ST_MenuInfo WHERE Menu_ID = 9000)
    INSERT INTO dbo.ST_MenuInfo (Menu_ID, System_No, Menu_No, Menu_Name, Para_ID, Call_Mode, Form_Name, Menu_Index)
    VALUES (9000, 'BS', 'BS', N'人脸通BS', -1, 1, NULL, 100);
IF NOT EXISTS (SELECT 1 FROM dbo.ST_MenuInfo WHERE Menu_ID = 9009)
    INSERT INTO dbo.ST_MenuInfo (Menu_ID, System_No, Menu_No, Menu_Name, Para_ID, Call_Mode, Form_Name, Menu_Index)
    VALUES (9009, 'BS', 'BS_SYSTEM', N'系统管理', 9000, 1, NULL, 9);
GO

DECLARE @Menus TABLE (
    Menu_ID INT NOT NULL,
    Menu_No VARCHAR(20) NOT NULL,
    Menu_Name NVARCHAR(20) NOT NULL,
    Form_Name VARCHAR(50) NOT NULL,
    Menu_Index TINYINT NOT NULL,
    Rights VARCHAR(10) NOT NULL
);
DECLARE @RightNames TABLE (Right_ID CHAR(1) PRIMARY KEY, Right_Name NVARCHAR(20), Suffix VARCHAR(20));
INSERT INTO @Menus (Menu_ID, Menu_No, Menu_Name, Form_Name, Menu_Index, Rights) VALUES
    (900901, 'BS_SYS_OPERATOR',     N'操作员管理',   '/system/operator',        1, '012357'),
    (900902, 'BS_SYS_ROLE',         N'角色管理',     '/system/role',            2, '012368'),
    (900903, 'BS_SYS_PERMISSION',   N'菜单与操作权限', '/system/permission',    3, '06'),
    (900904, 'BS_SYS_DEPTRIGHT',    N'部门数据权限', '/system/dept-right',      4, '06'),
    (900905, 'BS_SYS_DEVICERIGHT',  N'设备权限',     '/system/device-right',    5, '06'),
    (900906, 'BS_SYS_SUBSYSRIGHT',  N'子系统权限',   '/system/subsystem-right', 6, '06'),
    (900907, 'BS_SYS_CASHIERAUTH',  N'出纳授权管理', '/system/cashier-auth',    7, '06');
INSERT INTO @RightNames (Right_ID, Right_Name, Suffix) VALUES
    ('0', N'查看', 'view'),
    ('1', N'新增', 'create'),
    ('2', N'修改', 'update'),
    ('3', N'删除', 'delete'),
    ('5', N'重置密码', 'resetPwd'),
    ('6', N'分配权限', 'assign'),
    ('7', N'启停', 'status'),
    ('8', N'复制', 'copy');

DECLARE @MenuId INT, @MenuNo VARCHAR(20), @Rights VARCHAR(10), @RightId CHAR(1), @RightName NVARCHAR(20), @Suffix VARCHAR(20);
DECLARE @MenuName NVARCHAR(20), @FormName VARCHAR(50), @MenuIndex TINYINT;
DECLARE @Index INT, @Resource VARCHAR(30), @PermissionCode VARCHAR(64);
DECLARE menu_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT Menu_ID, Menu_No, Menu_Name, Form_Name, Menu_Index, Rights FROM @Menus;
OPEN menu_cursor;
FETCH NEXT FROM menu_cursor INTO @MenuId, @MenuNo, @MenuName, @FormName, @MenuIndex, @Rights;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.ST_MenuInfo WHERE Menu_ID = @MenuId)
        INSERT INTO dbo.ST_MenuInfo (Menu_ID, System_No, Menu_No, Menu_Name, Para_ID, Call_Mode, Form_Name, Menu_Index)
        VALUES (@MenuId, 'BS', @MenuNo, @MenuName, 9009, 1, @FormName, @MenuIndex);

    SET @Index = 1;
    WHILE @Index <= LEN(@Rights)
    BEGIN
        SET @RightId = SUBSTRING(@Rights, @Index, 1);
        SELECT @RightName = Right_Name, @Suffix = Suffix FROM @RightNames WHERE Right_ID = @RightId;
        IF NOT EXISTS (SELECT 1 FROM dbo.ST_RightValue WHERE Right_ID = @RightId AND Form_Name = @MenuNo)
            INSERT INTO dbo.ST_RightValue (Right_ID, Form_Name, Right_Name)
            VALUES (@RightId, @MenuNo, @RightName);
        SET @Index = @Index + 1;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.ST_RightInfo WHERE Menu_ID = @MenuId)
        INSERT INTO dbo.ST_RightInfo (Menu_ID, Form_Name, Right_Values, Remark)
        VALUES (@MenuId, @MenuNo, @Rights, N'BS页面权限位');
    ELSE
        UPDATE dbo.ST_RightInfo SET Right_Values = @Rights WHERE Menu_ID = @MenuId;

    SET @Resource = LOWER(SUBSTRING(@MenuNo, 8, 1)) + LOWER(SUBSTRING(@MenuNo, 9, 20));
    SET @Index = 1;
    WHILE @Index <= LEN(@Rights)
    BEGIN
        SET @RightId = SUBSTRING(@Rights, @Index, 1);
        SELECT @Suffix = Suffix FROM @RightNames WHERE Right_ID = @RightId;
        SET @PermissionCode = 'system:' + @Resource + ':' + @Suffix;
        IF NOT EXISTS (SELECT 1 FROM dbo.BS_RightCodeMap WHERE Menu_ID = @MenuId AND Right_ID = @RightId)
            INSERT INTO dbo.BS_RightCodeMap (Menu_ID, Right_ID, Permission_Code, Api_Route, Remark)
            VALUES (@MenuId, @RightId, @PermissionCode, NULL, N'BS权限码映射');
        SET @Index = @Index + 1;
    END

    IF EXISTS (SELECT 1 FROM dbo.ST_RoleInfo WHERE Role_No = 'Administrators')
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.ST_RoleRight WHERE Role_No = 'Administrators' AND Menu_ID = @MenuId)
            UPDATE dbo.ST_RoleRight SET Right_Values = @Rights
            WHERE Role_No = 'Administrators' AND Menu_ID = @MenuId;
        ELSE
            INSERT INTO dbo.ST_RoleRight (Role_No, Menu_ID, Right_Values)
            VALUES ('Administrators', @MenuId, @Rights);
    END

    FETCH NEXT FROM menu_cursor INTO @MenuId, @MenuNo, @MenuName, @FormName, @MenuIndex, @Rights;
END
CLOSE menu_cursor;
DEALLOCATE menu_cursor;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Permission.Version')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Permission.Version', '1', N'权限版本，授权变更后自增');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Schema.Version')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Schema.Version', '1', N'BS结构版本');
GO

/* =====================================================================================
   13. 系统配置与安全审计（对应《系统配置与安全审计详细设计文档》第十三章）
   原则：只增不删；新增对象一律 BS_ 前缀；新增列可空或带默认值；脚本可重复执行。
   ===================================================================================== */
GO

/*---------- 13.1 ST_BlackPerson 新增审计列（可空，对 CS 透明） ----------*/
IF COL_LENGTH('dbo.ST_BlackPerson', 'BS_User_No') IS NULL
    ALTER TABLE dbo.ST_BlackPerson ADD BS_User_No VARCHAR(20) NULL;
IF COL_LENGTH('dbo.ST_BlackPerson', 'BS_Add_Time') IS NULL
    ALTER TABLE dbo.ST_BlackPerson ADD BS_Add_Time DATETIME NULL;
IF COL_LENGTH('dbo.ST_BlackPerson', 'BS_Release_User') IS NULL
    ALTER TABLE dbo.ST_BlackPerson ADD BS_Release_User VARCHAR(20) NULL;
IF COL_LENGTH('dbo.ST_BlackPerson', 'BS_Release_Time') IS NULL
    ALTER TABLE dbo.ST_BlackPerson ADD BS_Release_Time DATETIME NULL;
GO

/*---------- 13.2 新增表 ----------*/
IF OBJECT_ID('dbo.BS_ParamMeta', 'U') IS NULL
CREATE TABLE dbo.BS_ParamMeta (
    Meta_ID          INT IDENTITY NOT NULL,
    Scope            VARCHAR(10) NOT NULL,
    Param_Code       VARCHAR(40) NOT NULL,
    Param_Name       VARCHAR(100) NULL,
    Param_Type       VARCHAR(20) NOT NULL CONSTRAINT DF_BS_ParamMeta_Type DEFAULT ('string'),
    Param_Options    VARCHAR(500) NULL,
    Default_Value    VARCHAR(100) NULL,
    Min_Value        VARCHAR(50) NULL,
    Max_Value        VARCHAR(50) NULL,
    Group_Name       VARCHAR(50) NULL,
    Is_ReadOnly      BIT NOT NULL CONSTRAINT DF_BS_ParamMeta_ReadOnly DEFAULT (0),
    Restart_Required BIT NOT NULL CONSTRAINT DF_BS_ParamMeta_Restart DEFAULT (0),
    Remark           VARCHAR(200) NULL,
    Update_User      VARCHAR(20) NULL,
    Update_Time      DATETIME NOT NULL CONSTRAINT DF_BS_ParamMeta_Time DEFAULT (GETDATE()),
    CONSTRAINT PK_BS_ParamMeta PRIMARY KEY CLUSTERED (Meta_ID),
    CONSTRAINT UQ_BS_ParamMeta UNIQUE (Scope, Param_Code)
);
GO

/* BS_ParamMeta.Param_Code 扩容到 40：Backup.Path.Whitelist 等点分长编码超过 20 字符。
   新库建表时已是 VARCHAR(40)，此段仅对历史库生效。
   用固定约束名 UQ_BS_ParamMeta（建表时定义），不用动态 SQL/QUOTENAME——
   DbUpdater 拆批执行，动态 EXEC 拼接在部分兼容级别上会报“QUOTENAME 附近有语法错误”导致服务起不来。 */
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.BS_ParamMeta')
      AND name = 'Param_Code'
      AND max_length <= 20
)
BEGIN
    IF OBJECT_ID('dbo.UQ_BS_ParamMeta', 'UQ') IS NOT NULL
        ALTER TABLE dbo.BS_ParamMeta DROP CONSTRAINT UQ_BS_ParamMeta;
    ALTER TABLE dbo.BS_ParamMeta ALTER COLUMN Param_Code VARCHAR(40) NOT NULL;
    ALTER TABLE dbo.BS_ParamMeta ADD CONSTRAINT UQ_BS_ParamMeta UNIQUE (Scope, Param_Code);
END
GO

IF OBJECT_ID('dbo.BS_ParamBackup', 'U') IS NULL
CREATE TABLE dbo.BS_ParamBackup (
    Backup_ID     INT IDENTITY NOT NULL,
    Scope         VARCHAR(10) NOT NULL,
    Backup_Name   VARCHAR(100) NOT NULL,
    Snapshot_Json NVARCHAR(MAX) NOT NULL,
    Item_Count    INT NOT NULL CONSTRAINT DF_BS_ParamBackup_Count DEFAULT (0),
    Hash          VARCHAR(64) NULL,
    Create_User   VARCHAR(20) NULL,
    Create_Time   DATETIME NOT NULL CONSTRAINT DF_BS_ParamBackup_Time DEFAULT (GETDATE()),
    Remark        VARCHAR(200) NULL,
    CONSTRAINT PK_BS_ParamBackup PRIMARY KEY CLUSTERED (Backup_ID)
);
GO

IF OBJECT_ID('dbo.BS_IntegrationEndpoint', 'U') IS NULL
CREATE TABLE dbo.BS_IntegrationEndpoint (
    ID_KEY              INT IDENTITY NOT NULL,
    Endpoint_Code       VARCHAR(50) NOT NULL,
    Endpoint_Name       VARCHAR(100) NOT NULL,
    Endpoint_Type       VARCHAR(20) NOT NULL,
    Base_Url            VARCHAR(300) NULL,
    Auth_Mode           VARCHAR(20) NULL CONSTRAINT DF_BS_Endpoint_Auth DEFAULT ('none'),
    Config_Json         NVARCHAR(MAX) NULL,
    Secret_Cipher       NVARCHAR(MAX) NULL,
    Secret_Updated_Time DATETIME NULL,
    Timeout_Sec         INT NULL CONSTRAINT DF_BS_Endpoint_Timeout DEFAULT (15),
    Retry_Count         TINYINT NULL CONSTRAINT DF_BS_Endpoint_Retry DEFAULT (1),
    Is_Enabled          BIT NOT NULL CONSTRAINT DF_BS_Endpoint_Enabled DEFAULT (1),
    Health_Status       TINYINT NOT NULL CONSTRAINT DF_BS_Endpoint_Health DEFAULT (0),
    Last_Check_Time     DATETIME NULL,
    Last_Check_Message  VARCHAR(500) NULL,
    Sort_No             INT NULL,
    Update_User         VARCHAR(20) NULL,
    Update_Time         DATETIME NOT NULL CONSTRAINT DF_BS_Endpoint_Time DEFAULT (GETDATE()),
    CONSTRAINT PK_BS_IntegrationEndpoint PRIMARY KEY CLUSTERED (ID_KEY),
    CONSTRAINT UQ_BS_IntegrationEndpoint UNIQUE (Endpoint_Code)
);
GO

IF OBJECT_ID('dbo.BS_IntegrationSecretHistory', 'U') IS NULL
CREATE TABLE dbo.BS_IntegrationSecretHistory (
    ID_KEY        INT IDENTITY NOT NULL,
    Endpoint_Code VARCHAR(50) NOT NULL,
    Secret_Cipher NVARCHAR(MAX) NOT NULL,
    Change_Reason VARCHAR(200) NULL,
    Change_User   VARCHAR(20) NULL,
    Change_Time   DATETIME NOT NULL CONSTRAINT DF_BS_SecretHis_Time DEFAULT (GETDATE()),
    CONSTRAINT PK_BS_IntegrationSecretHistory PRIMARY KEY CLUSTERED (ID_KEY)
);
GO

IF OBJECT_ID('dbo.BS_SyncTask', 'U') IS NULL
CREATE TABLE dbo.BS_SyncTask (
    Task_ID       INT IDENTITY NOT NULL,
    Task_Code     VARCHAR(50) NOT NULL,
    Task_Name     VARCHAR(100) NOT NULL,
    Task_Type     VARCHAR(30) NOT NULL,
    Endpoint_Code VARCHAR(50) NULL,
    Direction     VARCHAR(10) NOT NULL CONSTRAINT DF_BS_SyncTask_Dir DEFAULT ('down'),
    Cron_Expr     VARCHAR(50) NULL,
    Batch_Size    INT NULL CONSTRAINT DF_BS_SyncTask_Batch DEFAULT (200),
    Timeout_Sec   INT NULL CONSTRAINT DF_BS_SyncTask_Timeout DEFAULT (60),
    Is_Enabled    BIT NOT NULL CONSTRAINT DF_BS_SyncTask_Enabled DEFAULT (1),
    Last_Run_Time DATETIME NULL,
    Last_Status   TINYINT NULL,
    Last_Message  VARCHAR(500) NULL,
    Next_Run_Time DATETIME NULL,
    Update_User   VARCHAR(20) NULL,
    Update_Time   DATETIME NOT NULL CONSTRAINT DF_BS_SyncTask_Time DEFAULT (GETDATE()),
    CONSTRAINT PK_BS_SyncTask PRIMARY KEY CLUSTERED (Task_ID),
    CONSTRAINT UQ_BS_SyncTask UNIQUE (Task_Code)
);
GO

IF OBJECT_ID('dbo.BS_SyncLog', 'U') IS NULL
CREATE TABLE dbo.BS_SyncLog (
    ID_KEY        BIGINT IDENTITY NOT NULL,
    Task_Code     VARCHAR(50) NULL,
    Sync_Type     VARCHAR(30) NOT NULL,
    Direction     VARCHAR(10) NULL,
    Target        VARCHAR(100) NULL,
    Target_Key    VARCHAR(50) NULL,
    Status        TINYINT NOT NULL CONSTRAINT DF_BS_SyncLog_Status DEFAULT (0),
    Message       VARCHAR(1000) NULL,
    Request_Body  NVARCHAR(MAX) NULL,
    Response_Body NVARCHAR(MAX) NULL,
    Retry_Count   INT NOT NULL CONSTRAINT DF_BS_SyncLog_Retry DEFAULT (0),
    Elapsed       INT NULL,
    Trace_ID      VARCHAR(36) NULL,
    Create_Time   DATETIME NOT NULL CONSTRAINT DF_BS_SyncLog_Time DEFAULT (GETDATE()),
    Finish_Time   DATETIME NULL,
    CONSTRAINT PK_BS_SyncLog PRIMARY KEY CLUSTERED (ID_KEY)
);
GO

IF OBJECT_ID('dbo.BS_MaintJob', 'U') IS NULL
CREATE TABLE dbo.BS_MaintJob (
    Job_ID        INT IDENTITY NOT NULL,
    Job_Type      VARCHAR(30) NOT NULL,
    Job_Name      VARCHAR(100) NOT NULL,
    Params_Json   NVARCHAR(MAX) NULL,
    Status        TINYINT NOT NULL CONSTRAINT DF_BS_MaintJob_Status DEFAULT (0),
    Progress      INT NOT NULL CONSTRAINT DF_BS_MaintJob_Progress DEFAULT (0),
    Message       VARCHAR(1000) NULL,
    Backup_Job_ID INT NULL,
    Create_User   VARCHAR(20) NULL,
    Create_Time   DATETIME NOT NULL CONSTRAINT DF_BS_MaintJob_Time DEFAULT (GETDATE()),
    Start_Time    DATETIME NULL,
    Finish_Time   DATETIME NULL,
    Trace_ID      VARCHAR(36) NULL,
    CONSTRAINT PK_BS_MaintJob PRIMARY KEY CLUSTERED (Job_ID)
);
GO

IF OBJECT_ID('dbo.BS_MaintLog', 'U') IS NULL
CREATE TABLE dbo.BS_MaintLog (
    ID_KEY   BIGINT IDENTITY NOT NULL,
    Job_ID   INT NOT NULL,
    Step     VARCHAR(100) NULL,
    Message  VARCHAR(1000) NULL,
    Affected INT NULL,
    Log_Time DATETIME NOT NULL CONSTRAINT DF_BS_MaintLog_Time DEFAULT (GETDATE()),
    CONSTRAINT PK_BS_MaintLog PRIMARY KEY CLUSTERED (ID_KEY)
);
GO

IF OBJECT_ID('dbo.BS_LogPolicy', 'U') IS NULL
CREATE TABLE dbo.BS_LogPolicy (
    Policy_ID         INT IDENTITY NOT NULL,
    Log_Type          VARCHAR(30) NOT NULL,
    Retain_Days       INT NOT NULL CONSTRAINT DF_BS_LogPolicy_Days DEFAULT (365),
    Archive_Enabled   BIT NOT NULL CONSTRAINT DF_BS_LogPolicy_Archive DEFAULT (1),
    Last_Archive_Time DATETIME NULL,
    Update_User       VARCHAR(20) NULL,
    Update_Time       DATETIME NOT NULL CONSTRAINT DF_BS_LogPolicy_Time DEFAULT (GETDATE()),
    CONSTRAINT PK_BS_LogPolicy PRIMARY KEY CLUSTERED (Policy_ID),
    CONSTRAINT UQ_BS_LogPolicy UNIQUE (Log_Type)
);
GO

IF OBJECT_ID('dbo.BS_OperLogArchive', 'U') IS NULL
CREATE TABLE dbo.BS_OperLogArchive (
    ID_KEY       BIGINT NOT NULL,
    User_No      VARCHAR(20) NULL,
    User_Name    VARCHAR(20) NULL,
    Module       VARCHAR(30) NOT NULL,
    Action       VARCHAR(30) NOT NULL,
    Target       VARCHAR(100) NULL,
    Target_Key   VARCHAR(50) NULL,
    Before_Value NVARCHAR(MAX) NULL,
    After_Value  NVARCHAR(MAX) NULL,
    IP           VARCHAR(45) NULL,
    User_Agent   VARCHAR(200) NULL,
    Result       TINYINT NOT NULL,
    Message      VARCHAR(500) NULL,
    Trace_ID     VARCHAR(36) NULL,
    Opt_Time     DATETIME NOT NULL,
    Archive_Time DATETIME NOT NULL CONSTRAINT DF_BS_OperLogArc_Time DEFAULT (GETDATE()),
    CONSTRAINT PK_BS_OperLogArchive PRIMARY KEY CLUSTERED (ID_KEY, Archive_Time)
);
GO

IF OBJECT_ID('dbo.BS_LoginLogArchive', 'U') IS NULL
CREATE TABLE dbo.BS_LoginLogArchive (
    ID_KEY       BIGINT NOT NULL,
    User_No      VARCHAR(20) NULL,
    Login_Type   VARCHAR(20) NOT NULL,
    Result       TINYINT NOT NULL,
    Fail_Reason  VARCHAR(100) NULL,
    IP           VARCHAR(45) NULL,
    User_Agent   VARCHAR(200) NULL,
    Login_Time   DATETIME NOT NULL,
    Logout_Time  DATETIME NULL,
    Session_ID   VARCHAR(36) NULL,
    Archive_Time DATETIME NOT NULL CONSTRAINT DF_BS_LoginLogArc_Time DEFAULT (GETDATE()),
    CONSTRAINT PK_BS_LoginLogArchive PRIMARY KEY CLUSTERED (ID_KEY, Archive_Time)
);
GO

/*---------- 13.3 索引补充（全部 IX_BS_ 前缀，避免与 CS 索引重名） ----------*/
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_ST_SysParam_Code')
    CREATE INDEX IX_BS_ST_SysParam_Code ON dbo.ST_SysParam (Param_Code);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_ST_Dictionary_Table')
    CREATE INDEX IX_BS_ST_Dictionary_Table ON dbo.ST_Dictionary (Table_Name, Dict_Id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_ST_BlackPerson_Card')
    CREATE INDEX IX_BS_ST_BlackPerson_Card ON dbo.ST_BlackPerson (Card_No, Del_Date DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_ST_SysLog_Time')
    CREATE INDEX IX_BS_ST_SysLog_Time ON dbo.ST_SysLog (Opt_Time DESC) INCLUDE (User_Name, System_No);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_ST_SysLog_User')
    CREATE INDEX IX_BS_ST_SysLog_User ON dbo.ST_SysLog (User_Name, Opt_Time DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_ST_OptLog_Time')
    CREATE INDEX IX_BS_ST_OptLog_Time ON dbo.ST_OptLog (Opt_Time DESC) INCLUDE (User_Name, System_No);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_ST_OptLog_User')
    CREATE INDEX IX_BS_ST_OptLog_User ON dbo.ST_OptLog (User_Name, Opt_Time DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_ST_OptLog_Table')
    CREATE INDEX IX_BS_ST_OptLog_Table ON dbo.ST_OptLog (Opt_Table, Opt_Time DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_moc_upload_name_log_Time')
    CREATE INDEX IX_BS_moc_upload_name_log_Time ON dbo.st_moc_upload_name_log (upload_time DESC, state);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_WX_ApiLog_Time')
    CREATE INDEX IX_BS_WX_ApiLog_Time ON dbo.WX_ApiLog (Create_Time DESC, Success);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_DC_OrderDataLog_Time')
    CREATE INDEX IX_BS_DC_OrderDataLog_Time ON dbo.DC_OrderDataLog (Order_Time DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_ST_TrafficLog2201_Time')
    CREATE INDEX IX_BS_ST_TrafficLog2201_Time ON dbo.ST_TrafficLog2201 (Create_Date DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_XF_CollectLog_Time')
    CREATE INDEX IX_BS_XF_CollectLog_Time ON dbo.XF_CollectLog (Log_DateTime DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_SyncLog_Time')
    CREATE INDEX IX_BS_SyncLog_Time ON dbo.BS_SyncLog (Create_Time DESC, Status);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_SyncLog_Task')
    CREATE INDEX IX_BS_SyncLog_Task ON dbo.BS_SyncLog (Task_Code, Create_Time DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_MaintJob_Status')
    CREATE INDEX IX_BS_MaintJob_Status ON dbo.BS_MaintJob (Status, Create_Time DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_MaintLog_Job')
    CREATE INDEX IX_BS_MaintLog_Job ON dbo.BS_MaintLog (Job_ID, Log_Time);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_OperLog_Time')
    CREATE INDEX IX_BS_OperLog_Time ON dbo.BS_OperLog (Opt_Time DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_LoginLog_Time')
    CREATE INDEX IX_BS_LoginLog_Time ON dbo.BS_LoginLog (Login_Time DESC, Result);
GO

/*---------- 13.4 参数元数据种子（第四章 ST 作用域参数） ----------*/
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'SettleDate')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Default_Value, Group_Name, Remark)
    VALUES ('ST', 'SettleDate', N'财务结算日期', 'date', '2006-01-15', N'结算与清理', N'结算截止日期，不得晚于今天');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'ClearDate')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Default_Value, Group_Name, Remark)
    VALUES ('ST', 'ClearDate', N'数据清理日期', 'date', '2006-01-15', N'结算与清理', N'不得晚于结算日期');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'PERSON_VALIDTYPE')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name, Remark)
    VALUES ('ST', 'PERSON_VALIDTYPE', N'人员验证方式', 'enum', N'1=黑名单验证;2=红名单验证', '1', N'消费', N'CS 存储过程 ST_pd_GetNameList：1=黑名单(挂失)，2=红名单；默认 1');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'COM')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Default_Value, Group_Name, Restart_Required, Remark)
    VALUES ('ST', 'COM', N'读卡器串口号', 'string', 'COM1', N'卡务', 1, N'格式 COM+数字，默认 COM1');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'MODE_SF')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name, Restart_Required, Remark)
    VALUES ('ST', 'MODE_SF', N'售饭消费模式集合', 'enumSet', '0;1;2;3;4;5;6;255', '0;1;2;3;4;5;6;255', N'消费', 1, N'变更将影响终端，需下发后生效');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'MODE_LH')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name, Restart_Required, Remark)
    VALUES ('ST', 'MODE_LH', N'灵活消费模式集合', 'enumSet', '2;3;4;5;6', '2;3;4;5;6', N'消费', 1, N'变更将影响终端，需下发后生效');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'IsPersonNoAsCardNo')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name)
    VALUES ('ST', 'IsPersonNoAsCardNo', N'用人员编号作卡号', 'bool', '0;1', '0', N'消费');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'SubsidyInterval')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Default_Value, Min_Value, Max_Value, Group_Name)
    VALUES ('ST', 'SubsidyInterval', N'补贴领取间隔(单位秒)', 'int', '60', '0', '1440', N'补贴');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'IsAutoReadID')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name)
    VALUES ('ST', 'IsAutoReadID', N'是否自动读卡', 'bool', '0;1', '1', N'卡务');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'ExpenseIntervalTime')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Default_Value, Min_Value, Max_Value, Group_Name)
    VALUES ('ST', 'ExpenseIntervalTime', N'两次消费间隔(秒)', 'int', '3', '0', '3600', N'消费');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'PeriodBalance')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Default_Value, Group_Name, Remark)
    VALUES ('ST', 'PeriodBalance', N'截止时间点余额', 'decimal', '0', N'结算与清理', N'余额口径阈值，非开关；CS 默认 0，用途待现场确认');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'ConsumeHasSubsidy')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name)
    VALUES ('ST', 'ConsumeHasSubsidy', N'未领补贴不可消费', 'bool', '0;1', '0', N'补贴');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'Repeat.BeginTime')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Group_Name, Remark)
    VALUES ('ST', 'Repeat.BeginTime', N'重复消费记录过滤起始时间', 'datetime', N'消费', N'初始化取最大消费时间，无默认值');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'Fake.BeginTime')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Group_Name, Remark)
    VALUES ('ST', 'Fake.BeginTime', N'假写卡消费记录过滤起始时间', 'datetime', N'消费', N'初始化取最大消费时间，无默认值');
/* BS 作用域参数（写入 BS_SysConfig，不进入 CS 参数表） */
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'BS' AND Param_Code = 'Session.TimeoutMin')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Default_Value, Min_Value, Max_Value, Group_Name, Restart_Required)
    VALUES ('BS', 'Session.TimeoutMin', N'会话超时(分钟)', 'int', '60', '5', '480', N'运行', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'BS' AND Param_Code = 'Export.MaxRows')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Default_Value, Min_Value, Max_Value, Group_Name)
    VALUES ('BS', 'Export.MaxRows', N'导出上限行数', 'int', '50000', '1000', '200000', N'运行');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'BS' AND Param_Code = 'Log.RetainDays')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Default_Value, Min_Value, Max_Value, Group_Name)
    VALUES ('BS', 'Log.RetainDays', N'日志保留天数', 'int', '365', '30', '3650', N'运行');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'BS' AND Param_Code = 'Maintenance.Mode')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name, Restart_Required, Remark)
    VALUES ('BS', 'Maintenance.Mode', N'维护模式', 'bool', '0;1', '0', N'运行', 1, N'开启后除维护作业外写接口返回 40904，两小时后自动关闭');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'BS' AND Param_Code = 'Integration.Timeout')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Default_Value, Min_Value, Max_Value, Group_Name)
    VALUES ('BS', 'Integration.Timeout', N'集成接口超时(秒)', 'int', '15', '1', '120', N'运行');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'BS' AND Param_Code = 'Backup.Path.Whitelist')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Default_Value, Group_Name, Remark)
    VALUES ('BS', 'Backup.Path.Whitelist', N'备份路径白名单', 'string', 'D:\Backup;E:\Backup;C:\Backup;\\', N'备份', N'分号分隔的路径前缀，备份目标必须命中其一；留空则回退系统临时目录');
GO


/*===========================================================================
 13.4b 参数元数据补充登记（系统参数分组与名称，2026-11-18）
 原则：只动 BS_ParamMeta（BS 自有表），不改任何 CS 物理表的 Param_Name。
 幂等：全部使用 IF NOT EXISTS 插入，不含任何 UPDATE；
       需修正名称/选项的参数已直接合并进 13.4 原始种子，避免与已存在行冲突。
 说明：Param_Options 支持「值=标签」格式，前端按等号拆分显示中文标签，
       无等号时按裸值显示，保持对既有 0;1;2;3 的兼容。
===========================================================================*/

/*---------- 一、新增参数元数据：ST 作用域 ----------*/
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'CORP_NAME')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Group_Name, Remark)
    VALUES ('ST', 'CORP_NAME', N'公司名称', 'string', N'基础', N'实际存储于 ST_SysParam，由 XG 初始化脚本写入');

IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'LastBackupFileDir')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Group_Name, Remark)
    VALUES ('ST', 'LastBackupFileDir', N'上次备份备份数据库时的路径', 'string', N'结算与清理', N'标准脚本未预置，如现场库无此编码仅显示不保存');

/*---------- 二、新增参数元数据：DC 订餐作用域（物理表 ST_SysParam，前缀 DC.） ----------*/
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'DC' AND Param_Code = 'DC.Solution')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name, Remark)
    VALUES ('DC', 'DC.Solution', N'订餐模式', 'enum', N'0=消费扣款;1=订餐扣款', '0', N'订餐', N'CS 默认 0');

IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'DC' AND Param_Code = 'DC.IsUseMOCAsMDC')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name, Remark)
    VALUES ('DC', 'DC.IsUseMOCAsMDC', N'实时通讯下，用普通IC卡消费机作为订餐机', 'bool', N'0;1', '0', N'订餐', N'CS 默认 0');

/*---------- 三、新增参数元数据：MJ 门禁作用域（均在 MJ_SysParam 中已核实） ----------*/
-- 门禁数据清理
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'MJ' AND Param_Code = 'ABNORM_DAYS')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Default_Value, Group_Name, Remark)
    VALUES ('MJ', 'ABNORM_DAYS', N'异常刷卡保留天数', 'int', '15', N'门禁数据清理', NULL);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'MJ' AND Param_Code = 'ALARM_DAYS')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Default_Value, Group_Name, Remark)
    VALUES ('MJ', 'ALARM_DAYS', N'报警事件保留天数', 'int', '15', N'门禁数据清理', NULL);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'MJ' AND Param_Code = 'CLEAR_DATE')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Default_Value, Group_Name, Remark)
    VALUES ('MJ', 'CLEAR_DATE', N'清理过期数据起始日期', 'date', '2006-07-15', N'门禁数据清理', N'与 XG 作用域同名参数分开登记');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'MJ' AND Param_Code = 'CLEAR_DAYS')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Default_Value, Group_Name, Remark)
    VALUES ('MJ', 'CLEAR_DAYS', N'清理数据保留天数', 'int', '30', N'门禁数据清理', NULL);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'MJ' AND Param_Code = 'IS_AUTOCLEAR')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name, Remark)
    VALUES ('MJ', 'IS_AUTOCLEAR', N'是否自动清理过期数据', 'bool', N'0;1', '0', N'门禁数据清理', NULL);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'MJ' AND Param_Code = 'NORMAL_DAYS')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Default_Value, Group_Name, Remark)
    VALUES ('MJ', 'NORMAL_DAYS', N'正常刷卡保留天数', 'int', '7', N'门禁数据清理', NULL);

-- 门禁服务器
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'MJ' AND Param_Code = 'ACSPORT')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Default_Value, Group_Name, Remark)
    VALUES ('MJ', 'ACSPORT', N'门禁服务器通讯端口', 'int', '4003', N'门禁服务器', NULL);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'MJ' AND Param_Code = 'ACSSERVER')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Default_Value, Group_Name, Remark)
    VALUES ('MJ', 'ACSSERVER', N'门禁服务器地址', 'string', '127.0.0.1', N'门禁服务器', NULL);

-- 门禁参数
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'MJ' AND Param_Code = 'DVSPhotoStoreDir')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Group_Name, Remark)
    VALUES ('MJ', 'DVSPhotoStoreDir', N'DVS联动拍照图片存储位置', 'string', N'门禁参数', N'CS 默认值为空');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'MJ' AND Param_Code = 'CARDTOWIGAND26')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name, Remark)
    VALUES ('MJ', 'CARDTOWIGAND26', N'是否需要将卡号转换为微耕26格式', 'bool', N'0;1', '0', N'门禁参数', NULL);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'MJ' AND Param_Code = 'ISUSECARD_SN')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name, Remark)
    VALUES ('MJ', 'ISUSECARD_SN', N'是否采用IC卡序列号做门禁卡号', 'bool', N'0;1', '0', N'门禁参数', NULL);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'MJ' AND Param_Code = 'LAST_READCARD')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Group_Name, Remark)
    VALUES ('MJ', 'LAST_READCARD', N'最后一次门禁读头读到的卡号', 'string', N'门禁参数', NULL);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'MJ' AND Param_Code = 'PHOTO_DOORIN')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name, Remark)
    VALUES ('MJ', 'PHOTO_DOORIN', N'进门是否显示照片', 'bool', N'0;1', '1', N'门禁参数', NULL);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'MJ' AND Param_Code = 'PHOTO_DOOROUT')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name, Remark)
    VALUES ('MJ', 'PHOTO_DOOROUT', N'出门是否显示照片', 'bool', N'0;1', '1', N'门禁参数', NULL);

/*---------- 四、不登记的编码（已核对 CS 4.2.1 脚本，避免页面显示但保存报 40903） ----------
  Is_APB / Is_HiddenBack / CANOFFLINE：在 MJ_ExtendFunc 或 ST_MenuInfo 中，属功能开关/菜单，非 MJ_SysParam 参数。
  CABINET_URL / HasSencondPrice / IDCardLen / IsCheckCodeOnNew / gIsNoUseSmall /
  gIsWritePersonName / CtrlExt / PICSAVEPATH / SYS.ServicePhone / SYS.ServiceTime /
  SYS.ServiceWindow：标准脚本未找到，可能来自现场定制库；请先现场核实再手工补登。
  「子系统代码+CODE」类注册码：按前缀规则动态识别，不逐条登记。
  分组「未分组」保留不变，代表尚未登记元数据，与「其它」语义不同。
===========================================================================*/
GO

/*===========================================================================
 13.4c 参数元数据补充登记（26 条复核，2026-11-18）
 原则：只动 BS_ParamMeta，全部 IF NOT EXISTS 插入，不含 UPDATE。
 说明：参数列表以 CS 物理参数表为准（LoadScope 从 {ScopeDef.Table} 取行），
       元数据只补名称/分组/类型。标准脚本未预置的编码，现场库有才显示，
       现场库无则不会出现，登记无副作用。
===========================================================================*/

/*---------- 一、公共与卡务（ST，物理表 ST_SysParam） ----------*/
-- 智能柜系统首页
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'CABINET_URL')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Group_Name, Remark)
    VALUES ('ST', 'CABINET_URL', N'智能柜系统首页', 'string', N'基础', N'标准脚本未预置，现场库有此编码时才显示');

-- 是否有二次单价
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'HasSencondPrice')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name, Remark)
    VALUES ('ST', 'HasSencondPrice', N'是否有二次单价', 'bool', N'0;1', '0', N'消费', N'标准脚本未预置，现场库有此编码时才显示');

-- ID 读卡位数
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'IDCardLen')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Default_Value, Group_Name, Remark)
    VALUES ('ST', 'IDCardLen', N'ID读卡位数', 'int', '18', N'卡务', N'标准脚本未预置，现场库有此编码时才显示');

-- 发新卡时是否检查公司加密值
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'IsCheckCodeOnNew')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name, Remark)
    VALUES ('ST', 'IsCheckCodeOnNew', N'发新卡时是否检查公司加密值', 'bool', N'0;1', '0', N'卡务', N'标准脚本未预置，现场库有此编码时才显示');

-- 是否不采用小金额扇区
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'gIsNoUseSmall')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name, Remark)
    VALUES ('ST', 'gIsNoUseSmall', N'是否不采用小金额扇区(旧编码)', 'bool', N'0;1', '0', N'卡务', N'历史误登记：CS 真实编码为 IsNoUseSmall，此项仅现场库存在旧编码时才显示');

-- 是否写入人员姓名到 IC 卡中
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'gIsWritePersonName')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name, Remark)
    VALUES ('ST', 'gIsWritePersonName', N'是否写入人员姓名到IC卡中(旧编码)', 'bool', N'0;1', '0', N'卡务', N'历史误登记：CS 真实编码为 IsWritePersonName，此项仅现场库存在旧编码时才显示');

/*---------- 二、单位与客服信息（ST，与 CORP_NAME 同组，解决分组割裂） ----------*/
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'SYS.ServicePhone')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Group_Name, Remark)
    VALUES ('ST', 'SYS.ServicePhone', N'客服电话', 'string', N'基础', N'与 CORP_NAME 同组；标准脚本未预置');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'SYS.ServiceTime')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Group_Name, Remark)
    VALUES ('ST', 'SYS.ServiceTime', N'客服服务时间', 'string', N'基础', N'与 CORP_NAME 同组；标准脚本未预置');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'SYS.ServiceWindow')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Group_Name, Remark)
    VALUES ('ST', 'SYS.ServiceWindow', N'客服窗口', 'string', N'基础', N'与 CORP_NAME 同组；标准脚本未预置');

/*---------- 三、门禁参数（MJ，物理表 MJ_SysParam） ----------*/
-- 是否门禁一体机
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'MJ' AND Param_Code = 'CtrlExt')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name, Remark)
    VALUES ('MJ', 'CtrlExt', N'是否门禁一体机', 'bool', N'0;1', '0', N'门禁参数', N'标准脚本未预置，现场库有此编码时才显示');

-- 防尾随（CS MJ_SysParam 原名「防尾随」，默认 1）
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'MJ' AND Param_Code = 'Is_APB')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name, Remark)
    VALUES ('MJ', 'Is_APB', N'是否启用防尾随', 'bool', N'0;1', '1', N'门禁参数', N'CS MJ_SysParam 已有此编码（另在 MJ_ExtendFunc 有同名开关）');

-- 防潜回（CS MJ_SysParam 原名「防潜回」，默认 1）
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'MJ' AND Param_Code = 'Is_HiddenBack')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name, Remark)
    VALUES ('MJ', 'Is_HiddenBack', N'是否启用防潜回', 'bool', N'0;1', '1', N'门禁参数', N'CS MJ_SysParam 已有此编码');

-- 脱机时是否允许开门
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'MJ' AND Param_Code = 'CANOFFLINE')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name, Remark)
    VALUES ('MJ', 'CANOFFLINE', N'脱机时是否允许开门', 'bool', N'0;1', '0', N'门禁参数', N'标准脚本未预置，现场库有此编码时才显示');

-- 照片存储路径
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'MJ' AND Param_Code = 'PICSAVEPATH')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Group_Name, Remark)
    VALUES ('MJ', 'PICSAVEPATH', N'照片存储路径', 'string', N'门禁参数', N'标准脚本未预置，现场库有此编码时才显示');

/*---------- 四、子系统注册码（*_CODE）：由后端按前缀动态命名，不逐条登记 ----------
  后端 ParamsController.Merge / BsParamsController.Merge 增加兜底规则：
  Param_Code 以 _CODE 结尾且无元数据时，名称显示为「<子系统中文名>注册码」，
  分组归入「授权与注册」。例如 KQ_CODE -> 考勤注册码、MJ_CODE -> 门禁注册码。
  这样现场库新增子系统注册码时无需改种子数据。
===========================================================================*/
GO



/*---------- 13.4d 卡务类参数补充登记（CS 源码确认真实编码） ----------*/
/* IsNoUseSmall/IsWritePersonName/IsShowTakeAll 均为 ST_SysParam 中 Param_Code，
   源码 udmST.pas、uWriteCard.pas、uTakeFund.pas 通过 Param_Code 读取，归卡务组。 */
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'IsNoUseSmall')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name, Remark)
    VALUES ('ST', 'IsNoUseSmall', N'是否不采用小金额扇区', 'bool', '0;1', '0', N'卡务', N'CS udmST.pas：Param_Code=IsNoUseSmall，1=不采用小金额扇区');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'IsWritePersonName')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name, Remark)
    VALUES ('ST', 'IsWritePersonName', N'是否写入人员姓名', 'bool', '0;1', '0', N'卡务', N'CS uWriteCard.pas：Param_Name=IsWritePersonName，1=写姓名到IC卡');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_ParamMeta WHERE Scope = 'ST' AND Param_Code = 'IsShowTakeAll')
    INSERT INTO dbo.BS_ParamMeta (Scope, Param_Code, Param_Name, Param_Type, Param_Options, Default_Value, Group_Name, Remark)
    VALUES ('ST', 'IsShowTakeAll', N'是否显示全部取款', 'bool', '0;1', '0', N'卡务', N'CS uTakeFund.pas：IsShowTakeAll=1 时显示取款全部按钮');

/*---------- 13.5 集成端点、同步任务、日志策略种子 ----------*/
IF NOT EXISTS (SELECT 1 FROM dbo.BS_IntegrationEndpoint WHERE Endpoint_Code = 'DB_MAIN')
    INSERT INTO dbo.BS_IntegrationEndpoint (Endpoint_Code, Endpoint_Name, Endpoint_Type, Auth_Mode, Sort_No)
    VALUES ('DB_MAIN', N'主数据库', 'database', 'basic', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_IntegrationEndpoint WHERE Endpoint_Code = 'MOC_API')
    INSERT INTO dbo.BS_IntegrationEndpoint (Endpoint_Code, Endpoint_Name, Endpoint_Type, Auth_Mode, Sort_No)
    VALUES ('MOC_API', N'5501 设备接口服务', 'http', 'none', 2);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_IntegrationEndpoint WHERE Endpoint_Code = 'MOC_SVR')
    INSERT INTO dbo.BS_IntegrationEndpoint (Endpoint_Code, Endpoint_Name, Endpoint_Type, Auth_Mode, Sort_No)
    VALUES ('MOC_SVR', N'消费与考勤服务', 'http', 'basic', 3);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_IntegrationEndpoint WHERE Endpoint_Code = 'MOC_WS')
    INSERT INTO dbo.BS_IntegrationEndpoint (Endpoint_Code, Endpoint_Name, Endpoint_Type, Auth_Mode, Sort_No)
    VALUES ('MOC_WS', N'设备 WebSocket', 'http', 'none', 4);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_IntegrationEndpoint WHERE Endpoint_Code = 'WX_MP')
    INSERT INTO dbo.BS_IntegrationEndpoint (Endpoint_Code, Endpoint_Name, Endpoint_Type, Auth_Mode, Sort_No)
    VALUES ('WX_MP', N'微信公众号', 'wechat', 'oauth', 5);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_IntegrationEndpoint WHERE Endpoint_Code = 'WX_MINI')
    INSERT INTO dbo.BS_IntegrationEndpoint (Endpoint_Code, Endpoint_Name, Endpoint_Type, Auth_Mode, Sort_No)
    VALUES ('WX_MINI', N'微信小程序', 'wechat', 'oauth', 6);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_IntegrationEndpoint WHERE Endpoint_Code = 'WX_PAY')
    INSERT INTO dbo.BS_IntegrationEndpoint (Endpoint_Code, Endpoint_Name, Endpoint_Type, Auth_Mode, Sort_No)
    VALUES ('WX_PAY', N'微信支付', 'payment', 'cert', 7);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_IntegrationEndpoint WHERE Endpoint_Code = 'ALI_SMS')
    INSERT INTO dbo.BS_IntegrationEndpoint (Endpoint_Code, Endpoint_Name, Endpoint_Type, Auth_Mode, Sort_No)
    VALUES ('ALI_SMS', N'阿里云短信', 'sms', 'apikey', 8);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_IntegrationEndpoint WHERE Endpoint_Code = 'VISITOR_QR')
    INSERT INTO dbo.BS_IntegrationEndpoint (Endpoint_Code, Endpoint_Name, Endpoint_Type, Auth_Mode, Sort_No)
    VALUES ('VISITOR_QR', N'访客二维码', 'http', 'sign', 9);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_IntegrationEndpoint WHERE Endpoint_Code = 'ST_API')
    INSERT INTO dbo.BS_IntegrationEndpoint (Endpoint_Code, Endpoint_Name, Endpoint_Type, Auth_Mode, Sort_No)
    VALUES ('ST_API', N'对外标准接口', 'http', 'apikey', 10);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.BS_SyncTask WHERE Task_Code = 'PERSON_NAME_DOWN')
    INSERT INTO dbo.BS_SyncTask (Task_Code, Task_Name, Task_Type, Endpoint_Code, Direction, Batch_Size)
    VALUES ('PERSON_NAME_DOWN', N'人员名单下发', 'person', 'MOC_API', 'down', 200);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SyncTask WHERE Task_Code = 'CARD_INFO_DOWN')
    INSERT INTO dbo.BS_SyncTask (Task_Code, Task_Name, Task_Type, Endpoint_Code, Direction, Batch_Size)
    VALUES ('CARD_INFO_DOWN', N'卡信息下发', 'card', 'MOC_API', 'down', 200);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SyncTask WHERE Task_Code = 'FACE_INDEX_DOWN')
    INSERT INTO dbo.BS_SyncTask (Task_Code, Task_Name, Task_Type, Endpoint_Code, Direction, Batch_Size)
    VALUES ('FACE_INDEX_DOWN', N'人脸特征索引下发', 'face', 'MOC_API', 'down', 100);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SyncTask WHERE Task_Code = 'ACCESS_RIGHT_DOWN')
    INSERT INTO dbo.BS_SyncTask (Task_Code, Task_Name, Task_Type, Endpoint_Code, Direction, Batch_Size)
    VALUES ('ACCESS_RIGHT_DOWN', N'门禁权限下发', 'access', 'MOC_API', 'down', 200);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SyncTask WHERE Task_Code = 'CONSUME_UP')
    INSERT INTO dbo.BS_SyncTask (Task_Code, Task_Name, Task_Type, Endpoint_Code, Direction, Batch_Size)
    VALUES ('CONSUME_UP', N'消费流水采集', 'person', 'MOC_SVR', 'up', 500);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SyncTask WHERE Task_Code = 'ATTENDANCE_UP')
    INSERT INTO dbo.BS_SyncTask (Task_Code, Task_Name, Task_Type, Endpoint_Code, Direction, Batch_Size)
    VALUES ('ATTENDANCE_UP', N'考勤流水采集', 'attendance', 'MOC_SVR', 'up', 500);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SyncTask WHERE Task_Code = 'ORDER_DOWN')
    INSERT INTO dbo.BS_SyncTask (Task_Code, Task_Name, Task_Type, Endpoint_Code, Direction, Batch_Size)
    VALUES ('ORDER_DOWN', N'订餐订单下发', 'order', 'MOC_API', 'down', 200);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SyncTask WHERE Task_Code = 'WX_RECONCILE')
    INSERT INTO dbo.BS_SyncTask (Task_Code, Task_Name, Task_Type, Endpoint_Code, Direction, Batch_Size)
    VALUES ('WX_RECONCILE', N'微信订单对账', 'wechat', 'WX_PAY', 'up', 100);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SyncTask WHERE Task_Code = 'SMS_SEND')
    INSERT INTO dbo.BS_SyncTask (Task_Code, Task_Name, Task_Type, Endpoint_Code, Direction, Batch_Size)
    VALUES ('SMS_SEND', N'短信发送', 'sms', 'ALI_SMS', 'down', 100);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SyncTask WHERE Task_Code = 'LOG_ARCHIVE')
    INSERT INTO dbo.BS_SyncTask (Task_Code, Task_Name, Task_Type, Endpoint_Code, Direction, Batch_Size)
    VALUES ('LOG_ARCHIVE', N'日志归档', 'system', 'DB_MAIN', 'up', 500);
GO

-- 迁移：历史库的 LOG_ARCHIVE 未绑定端点，补挂到 DB_MAIN，使其能在"接口与集成"页显示并执行。
-- 仅补 Endpoint_Code IS NULL 的记录，不覆盖现场已绑定的值；可反复执行。
UPDATE dbo.BS_SyncTask
   SET Endpoint_Code = 'DB_MAIN', Update_Time = GETDATE()
 WHERE Task_Code = 'LOG_ARCHIVE' AND Endpoint_Code IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.BS_LogPolicy WHERE Log_Type = 'OPER')
    INSERT INTO dbo.BS_LogPolicy (Log_Type, Retain_Days, Archive_Enabled) VALUES ('OPER', 1095, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_LogPolicy WHERE Log_Type = 'LOGIN')
    INSERT INTO dbo.BS_LogPolicy (Log_Type, Retain_Days, Archive_Enabled) VALUES ('LOGIN', 365, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_LogPolicy WHERE Log_Type = 'SYNC')
    INSERT INTO dbo.BS_LogPolicy (Log_Type, Retain_Days, Archive_Enabled) VALUES ('SYNC', 180, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.BS_LogPolicy WHERE Log_Type = 'SYSTEM')
    INSERT INTO dbo.BS_LogPolicy (Log_Type, Retain_Days, Archive_Enabled) VALUES ('SYSTEM', 365, 1);
GO

/*---------- 13.6 菜单、权限位、权限码映射 ----------*/
IF NOT EXISTS (SELECT 1 FROM dbo.ST_MenuInfo WHERE Menu_ID = 900910)
    INSERT INTO dbo.ST_MenuInfo (Menu_ID, System_No, Menu_No, Menu_Name, Para_ID, Call_Mode, Form_Name, Menu_Index)
    VALUES (900910, 'BS', 'BS_SYSCONFIG', N'系统配置', 9009, 1, NULL, 10);
IF NOT EXISTS (SELECT 1 FROM dbo.ST_MenuInfo WHERE Menu_ID = 900920)
    INSERT INTO dbo.ST_MenuInfo (Menu_ID, System_No, Menu_No, Menu_Name, Para_ID, Call_Mode, Form_Name, Menu_Index)
    VALUES (900920, 'BS', 'BS_SECAUDIT', N'安全审计', 9009, 1, NULL, 11);
GO

DECLARE @Pages TABLE (
    Menu_ID    INT NOT NULL,
    Menu_No    VARCHAR(20) NOT NULL,
    Menu_Name  NVARCHAR(20) NOT NULL,
    Form_Name  VARCHAR(50) NOT NULL,
    Parent_ID  INT NOT NULL,
    Menu_Index TINYINT NOT NULL
);
INSERT INTO @Pages (Menu_ID, Menu_No, Menu_Name, Form_Name, Parent_ID, Menu_Index) VALUES
    (900911, 'BS_SYS_PARAM',       N'系统参数',   '/system/param',       900910, 1),
    (900912, 'BS_SYS_DICT',        N'数据字典',   '/system/dict',        900910, 2),
    (900913, 'BS_SYS_INTEGRATION', N'接口与集成', '/system/integration', 900910, 3),
    (900914, 'BS_SYS_MAINTAIN',    N'数据维护',   '/system/maintain',    900910, 4),
    (900915, 'BS_SYS_BLACKLIST',   N'黑名单管理', '/system/blacklist',   900910, 5),
    (900921, 'BS_LOG_SYSTEM',      N'系统日志',   '/system/log/system',  900920, 1),
    (900922, 'BS_LOG_LOGIN',       N'登录日志',   '/system/log/login',   900920, 2),
    (900923, 'BS_LOG_OPER',        N'操作审计',   '/system/log/oper',    900920, 3),
    (900924, 'BS_LOG_SYNC',        N'同步日志',   '/system/log/sync',    900920, 4);

DECLARE @PageRights TABLE (
    Menu_ID         INT NOT NULL,
    Right_ID        CHAR(1) NOT NULL,
    Right_Name      NVARCHAR(20) NOT NULL,
    Permission_Code VARCHAR(64) NOT NULL,
    Sort_No         TINYINT NOT NULL
);
INSERT INTO @PageRights (Menu_ID, Right_ID, Right_Name, Permission_Code, Sort_No) VALUES
    (900911, '0', N'查看',     'system:param:view',    1),
    (900911, '2', N'修改',     'system:param:update',  2),
    (900911, '6', N'备份',     'system:param:backup',  3),
    (900911, '7', N'恢复默认', 'system:param:restore', 4),
    (900911, '8', N'导入',     'system:param:import',  5),
    (900911, '4', N'导出',     'system:param:export',  6),
    (900912, '0', N'查看',     'system:dict:view',     1),
    (900912, '1', N'新增',     'system:dict:create',   2),
    (900912, '2', N'修改',     'system:dict:update',   3),
    (900912, '3', N'删除',     'system:dict:delete',   4),
    (900912, '9', N'同步',     'system:dict:sync',     5),
    (900912, '4', N'导出',     'system:dict:export',   6),
    (900913, '0', N'查看',     'system:integration:view',   1),
    (900913, '2', N'修改',     'system:integration:update', 2),
    (900913, '5', N'连通测试', 'system:integration:test',   3),
    (900913, '7', N'启停',     'system:integration:toggle', 4),
    (900914, '0', N'查看',     'system:maintain:view',    1),
    (900914, '6', N'备份',     'system:maintain:backup',  2),
    (900914, '1', N'整理',     'system:maintain:rebuild', 3),
    (900914, '3', N'清理',     'system:maintain:cleanup', 4),
    (900914, '5', N'执行',     'system:maintain:execute', 5),
    (900915, '0', N'查看',     'system:blacklist:view',   1),
    (900915, '1', N'加入',     'system:blacklist:create', 2),
    (900915, '3', N'释放',     'system:blacklist:delete', 3),
    (900915, '4', N'导出',     'system:blacklist:export', 4),
    (900921, '0', N'查看',     'system:log:system',        1),
    (900921, '4', N'导出',     'system:log:system:export', 2),
    (900922, '0', N'查看',     'system:log:login',         1),
    (900922, '4', N'导出',     'system:log:login:export',  2),
    (900923, '0', N'查看',     'system:log:oper',          1),
    (900923, '4', N'导出',     'system:log:oper:export',   2),
    (900924, '0', N'查看',     'system:log:sync',          1),
    (900924, '4', N'导出',     'system:log:sync:export',   2),
    (900924, '5', N'重试',     'system:log:sync:retry',    3);

DECLARE @MenuId INT, @MenuNo VARCHAR(20), @MenuName NVARCHAR(20), @FormName VARCHAR(50), @Parent INT, @MenuIndex TINYINT;
DECLARE @RightId CHAR(1), @RightName NVARCHAR(20), @Code VARCHAR(64);
DECLARE @Rights VARCHAR(20);

DECLARE page_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT Menu_ID, Menu_No, Menu_Name, Form_Name, Parent_ID, Menu_Index FROM @Pages ORDER BY Menu_ID;
OPEN page_cursor;
FETCH NEXT FROM page_cursor INTO @MenuId, @MenuNo, @MenuName, @FormName, @Parent, @MenuIndex;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.ST_MenuInfo WHERE Menu_ID = @MenuId)
        INSERT INTO dbo.ST_MenuInfo (Menu_ID, System_No, Menu_No, Menu_Name, Para_ID, Call_Mode, Form_Name, Menu_Index)
        VALUES (@MenuId, 'BS', @MenuNo, @MenuName, @Parent, 1, @FormName, @MenuIndex);

    SELECT @Rights = STUFF((
        SELECT ',' + Right_ID FROM @PageRights WHERE Menu_ID = @MenuId ORDER BY Right_ID FOR XML PATH('')
    ), 1, 1, '');
    SET @Rights = REPLACE(ISNULL(@Rights, ''), ',', '');

    IF NOT EXISTS (SELECT 1 FROM dbo.ST_RightInfo WHERE Menu_ID = @MenuId)
        INSERT INTO dbo.ST_RightInfo (Menu_ID, Form_Name, Right_Values, Remark)
        VALUES (@MenuId, @MenuNo, @Rights, N'BS系统配置与安全审计权限位');
    ELSE
        UPDATE dbo.ST_RightInfo SET Right_Values = @Rights WHERE Menu_ID = @MenuId;

    DECLARE right_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT Right_ID, Right_Name, Permission_Code FROM @PageRights WHERE Menu_ID = @MenuId ORDER BY Sort_No;
    OPEN right_cursor;
    FETCH NEXT FROM right_cursor INTO @RightId, @RightName, @Code;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.ST_RightValue WHERE Right_ID = @RightId AND Form_Name = @MenuNo)
            INSERT INTO dbo.ST_RightValue (Right_ID, Form_Name, Right_Name)
            VALUES (@RightId, @MenuNo, @RightName);
        IF NOT EXISTS (SELECT 1 FROM dbo.BS_RightCodeMap WHERE Menu_ID = @MenuId AND Right_ID = @RightId)
            INSERT INTO dbo.BS_RightCodeMap (Menu_ID, Right_ID, Permission_Code, Api_Route, Remark)
            VALUES (@MenuId, @RightId, @Code, NULL, N'BS系统配置与安全审计权限码');
        FETCH NEXT FROM right_cursor INTO @RightId, @RightName, @Code;
    END
    CLOSE right_cursor;
    DEALLOCATE right_cursor;

    IF EXISTS (SELECT 1 FROM dbo.ST_RoleInfo WHERE Role_No = 'Administrators')
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.ST_RoleRight WHERE Role_No = 'Administrators' AND Menu_ID = @MenuId)
            UPDATE dbo.ST_RoleRight SET Right_Values = @Rights
            WHERE Role_No = 'Administrators' AND Menu_ID = @MenuId;
        ELSE
            INSERT INTO dbo.ST_RoleRight (Role_No, Menu_ID, Right_Values)
            VALUES ('Administrators', @MenuId, @Rights);
    END

    FETCH NEXT FROM page_cursor INTO @MenuId, @MenuNo, @MenuName, @FormName, @Parent, @MenuIndex;
END
CLOSE page_cursor;
DEALLOCATE page_cursor;
GO

/*---------- 13.7 运行配置初始化 ----------*/
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Config.Version')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Config.Version', '1', N'系统配置版本，参数/字典/端点变更后自增');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Maintenance.Mode')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Maintenance.Mode', '0', N'维护模式：0 关闭、1 开启');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Maintenance.Mode.Until')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Maintenance.Mode.Until', '', N'维护模式自动关闭时间');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Backup.Path.Whitelist')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Backup.Path.Whitelist', 'D:\Backup;E:\Backup;C:\Backup;\\', N'备份目标路径白名单，分号分隔');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Config.CipherKey')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Config.CipherKey', '', N'集成密钥加密材料（Base64，优先取环境变量 BS_CONFIG_KEY）');
GO

/*=====================================================================
  14. 卡务中心-人员与组织：扩展表、索引、菜单与权限（可重复执行）
  依据《人脸通BS系统-卡务中心-人员与组织详细设计文档》第九章
  原则：只增不删，主数据仍写 CS 既有表，扩展信息写 BS_ 表
=====================================================================*/

/*---------- 14.1 新增表 ----------*/
IF OBJECT_ID('dbo.BS_PersonExt', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BS_PersonExt (
        Person_ID            INT NOT NULL,
        BS_Source            VARCHAR(20) NULL CONSTRAINT DF_BS_PersonExt_Source DEFAULT ('BS'),
        BS_Import_Batch      VARCHAR(36) NULL,
        BS_Face_Status       TINYINT NOT NULL CONSTRAINT DF_BS_PersonExt_Face DEFAULT (0),
        BS_Photo_Review_User VARCHAR(20) NULL,
        BS_Photo_Review_Time DATETIME NULL,
        BS_Photo_Review_Reason NVARCHAR(200) NULL,
        BS_Create_User       VARCHAR(20) NULL,
        BS_Create_Time       DATETIME NULL,
        BS_Update_User       VARCHAR(20) NULL,
        BS_Update_Time       DATETIME NULL,
        CONSTRAINT PK_BS_PersonExt PRIMARY KEY CLUSTERED (Person_ID)
    )
END
GO

/* 驳回原因：老库补列，可重复执行 */
IF COL_LENGTH('dbo.BS_PersonExt', 'BS_Photo_Review_Reason') IS NULL
    ALTER TABLE dbo.BS_PersonExt ADD BS_Photo_Review_Reason NVARCHAR(200) NULL;
GO

IF OBJECT_ID('dbo.BS_PhotoHistory', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BS_PhotoHistory (
        ID_KEY      BIGINT IDENTITY NOT NULL,
        Person_ID   INT NOT NULL,
        Photo       VARBINARY(MAX) NULL,
        Id_Photo    VARBINARY(MAX) NULL,
        Create_User VARCHAR(20) NULL,
        Create_Time DATETIME NOT NULL CONSTRAINT DF_BS_PhotoHistory_Time DEFAULT (GETDATE()),
        Remark      VARCHAR(200) NULL,
        CONSTRAINT PK_BS_PhotoHistory PRIMARY KEY CLUSTERED (ID_KEY)
    )
END
GO

IF OBJECT_ID('dbo.BS_ImportJob', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BS_ImportJob (
        Job_ID       BIGINT IDENTITY NOT NULL,
        Job_Type     VARCHAR(20) NOT NULL CONSTRAINT DF_BS_ImportJob_Type DEFAULT ('PERSON'),
        File_Name    VARCHAR(200) NULL,
        File_Hash    VARCHAR(64) NULL,
        Mode         VARCHAR(20) NULL CONSTRAINT DF_BS_ImportJob_Mode DEFAULT ('CREATE'),
        Status       TINYINT NOT NULL CONSTRAINT DF_BS_ImportJob_Status DEFAULT (0),
        Total_Rows   INT NOT NULL CONSTRAINT DF_BS_ImportJob_Total DEFAULT (0),
        Success_Rows INT NOT NULL CONSTRAINT DF_BS_ImportJob_Success DEFAULT (0),
        Fail_Rows    INT NOT NULL CONSTRAINT DF_BS_ImportJob_Fail DEFAULT (0),
        Skip_Rows    INT NOT NULL CONSTRAINT DF_BS_ImportJob_Skip DEFAULT (0),
        Options_Json NVARCHAR(MAX) NULL,
        Error_File   VARCHAR(300) NULL,
        Create_User  VARCHAR(20) NULL,
        Create_Time  DATETIME NOT NULL CONSTRAINT DF_BS_ImportJob_Time DEFAULT (GETDATE()),
        Finish_Time  DATETIME NULL,
        CONSTRAINT PK_BS_ImportJob PRIMARY KEY CLUSTERED (Job_ID)
    )
END
GO

IF OBJECT_ID('dbo.BS_ImportRow', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BS_ImportRow (
        ID_KEY        BIGINT IDENTITY NOT NULL,
        Job_ID        BIGINT NOT NULL,
        Row_No        INT NOT NULL,
        Person_No     VARCHAR(20) NULL,
        Card_No       VARCHAR(12) NULL,
        Person_Name   VARCHAR(20) NULL,
        Action        VARCHAR(20) NULL,
        Status        TINYINT NOT NULL CONSTRAINT DF_BS_ImportRow_Status DEFAULT (0),
        Error_Code    VARCHAR(20) NULL,
        Error_Message VARCHAR(500) NULL,
        Person_ID     INT NULL,
        Raw_Json      NVARCHAR(MAX) NULL,
        Create_Time   DATETIME NOT NULL CONSTRAINT DF_BS_ImportRow_Time DEFAULT (GETDATE()),
        CONSTRAINT PK_BS_ImportRow PRIMARY KEY CLUSTERED (ID_KEY)
    )
END
GO

/*---------- 14.2 索引补充 ----------*/
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_ST_Person_Dept' AND object_id = OBJECT_ID('ST_Person'))
    CREATE INDEX IX_BS_ST_Person_Dept ON dbo.ST_Person (Dept_ID, Is_Del)
        INCLUDE (Person_No, Person_Name, Card_No, mobile);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_ST_Person_Name' AND object_id = OBJECT_ID('ST_Person'))
    CREATE INDEX IX_BS_ST_Person_Name ON dbo.ST_Person (Person_Name);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_ST_PersonPhoto_Update' AND object_id = OBJECT_ID('ST_PersonPhoto'))
    CREATE INDEX IX_BS_ST_PersonPhoto_Update ON dbo.ST_PersonPhoto (update_time DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_ST_Dictionary_Table' AND object_id = OBJECT_ID('ST_Dictionary'))
    CREATE INDEX IX_BS_ST_Dictionary_Table ON dbo.ST_Dictionary (Table_Name, Dict_Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_PhotoHistory_Person' AND object_id = OBJECT_ID('BS_PhotoHistory'))
    CREATE INDEX IX_BS_PhotoHistory_Person ON dbo.BS_PhotoHistory (Person_ID, Create_Time DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_ImportJob_Time' AND object_id = OBJECT_ID('BS_ImportJob'))
    CREATE INDEX IX_BS_ImportJob_Time ON dbo.BS_ImportJob (Create_Time DESC, Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_ImportRow_Job' AND object_id = OBJECT_ID('BS_ImportRow'))
    CREATE INDEX IX_BS_ImportRow_Job ON dbo.BS_ImportRow (Job_ID, Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_PersonExt_Face' AND object_id = OBJECT_ID('BS_PersonExt'))
    CREATE INDEX IX_BS_PersonExt_Face ON dbo.BS_PersonExt (BS_Face_Status);
GO

/*---------- 14.3 卡务中心菜单 ----------*/
IF NOT EXISTS (SELECT 1 FROM dbo.ST_MenuInfo WHERE Menu_ID = 9001)
    INSERT INTO dbo.ST_MenuInfo (Menu_ID, System_No, Menu_No, Menu_Name, Para_ID, Call_Mode, Form_Name, Menu_Index)
    VALUES (9001, 'BS', 'BS_CARD', N'卡务中心', 9000, 1, NULL, 10);
GO

DECLARE @CardPages TABLE (
    Menu_ID INT NOT NULL,
    Menu_No VARCHAR(20) NOT NULL,
    Menu_Name NVARCHAR(20) NOT NULL,
    Form_Name VARCHAR(50) NOT NULL,
    Menu_Index TINYINT NOT NULL
);
INSERT INTO @CardPages (Menu_ID, Menu_No, Menu_Name, Form_Name, Menu_Index) VALUES
    (910101, 'BS_CARD_PERSON', N'人员档案',      '/card/person', 1),
    (910102, 'BS_CARD_DEPT',   N'部门档案',      '/card/dept',   2),
    (910103, 'BS_CARD_CODE',   N'编码管理',      '/card/code',   3),
    (910104, 'BS_CARD_FACE',   N'人脸照片库',    '/card/face',   4),
    (910105, 'BS_CARD_IMPORT', N'人员导入导出',  '/card/import', 5);

INSERT INTO dbo.ST_MenuInfo (Menu_ID, System_No, Menu_No, Menu_Name, Para_ID, Call_Mode, Form_Name, Menu_Index)
SELECT p.Menu_ID, 'BS', p.Menu_No, p.Menu_Name, 9001, 1, p.Form_Name, p.Menu_Index
  FROM @CardPages p
 WHERE NOT EXISTS (SELECT 1 FROM dbo.ST_MenuInfo m WHERE m.Menu_ID = p.Menu_ID);
GO

/*---------- 14.4 权限位与权限码映射 ----------*/
DECLARE @CardRights TABLE (
    Menu_ID INT NOT NULL,
    Right_ID CHAR(1) NOT NULL,
    Right_Name NVARCHAR(20) NOT NULL,
    Permission_Code VARCHAR(64) NOT NULL,
    Sort_No TINYINT NOT NULL
);
INSERT INTO @CardRights (Menu_ID, Right_ID, Right_Name, Permission_Code, Sort_No) VALUES
    (9001,   '0', N'查看',     'card:view',                1),
    (910101, '0', N'查看',     'card:person:view',         1),
    (910101, '1', N'新增',     'card:person:create',       2),
    (910101, '2', N'修改',     'card:person:update',       3),
    (910101, '3', N'注销',     'card:person:delete',       4),
    (910101, '4', N'导出',     'card:person:export',       5),
    (910101, '5', N'照片维护', 'card:person:photo',        6),
    (910101, '6', N'敏感字段', 'card:person:sensitive',    7),
    (910102, '0', N'查看',     'card:dept:view',           1),
    (910102, '1', N'新增',     'card:dept:create',         2),
    (910102, '2', N'修改',     'card:dept:update',         3),
    (910102, '3', N'停用',     'card:dept:delete',         4),
    (910102, '4', N'导出',     'card:dept:export',         5),
    (910103, '0', N'查看',     'card:code:view',           1),
    (910103, '1', N'新增',     'card:code:create',         2),
    (910103, '2', N'修改',     'card:code:update',         3),
    (910103, '3', N'删除',     'card:code:delete',         4),
    (910104, '0', N'查看',     'card:face:view',           1),
    (910104, '1', N'上传',     'card:face:upload',         2),
    (910104, '2', N'审核',     'card:face:review',         3),
    (910104, '3', N'删除',     'card:face:delete',         4),
    (910104, '4', N'导出原图', 'card:face:export',         5),
    (910105, '0', N'查看',     'card:import:view',         1),
    (910105, '1', N'导入',     'card:import:import',       2),
    (910105, '2', N'导出',     'card:import:export',       3),
    (910105, '3', N'下载模板', 'card:import:template',     4),
    (910105, '4', N'失败重试', 'card:import:retry',        5);

DECLARE @MenuId INT, @MenuNo VARCHAR(20), @Rights VARCHAR(20);
DECLARE @RightId CHAR(1), @RightName NVARCHAR(20), @Code VARCHAR(64);

DECLARE right_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT Menu_ID, Right_ID, Right_Name, Permission_Code FROM @CardRights ORDER BY Menu_ID, Sort_No;
OPEN right_cursor;
FETCH NEXT FROM right_cursor INTO @MenuId, @RightId, @RightName, @Code;
WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT @MenuNo = Menu_No FROM dbo.ST_MenuInfo WHERE Menu_ID = @MenuId;

    IF @MenuNo IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.ST_RightValue WHERE Right_ID = @RightId AND Form_Name = @MenuNo)
            INSERT INTO dbo.ST_RightValue (Right_ID, Form_Name, Right_Name)
            VALUES (@RightId, @MenuNo, @RightName);

        IF NOT EXISTS (SELECT 1 FROM dbo.BS_RightCodeMap WHERE Menu_ID = @MenuId AND Right_ID = @RightId)
            INSERT INTO dbo.BS_RightCodeMap (Menu_ID, Right_ID, Permission_Code, Api_Route, Remark)
            VALUES (@MenuId, @RightId, @Code, NULL, N'BS卡务中心人员与组织权限码');
    END

    FETCH NEXT FROM right_cursor INTO @MenuId, @RightId, @RightName, @Code;
END
CLOSE right_cursor;
DEALLOCATE right_cursor;

/* 汇总每个菜单的权限位字符串 */
UPDATE ri
   SET ri.Right_Values = x.Rights
  FROM dbo.ST_RightInfo ri
  JOIN (
        SELECT Menu_ID, STUFF((SELECT '' + r.Right_ID
                                 FROM @CardRights r
                                WHERE r.Menu_ID = c.Menu_ID
                                ORDER BY r.Right_ID
                                FOR XML PATH('')), 1, 0, '') AS Rights
          FROM @CardRights c
         GROUP BY c.Menu_ID
       ) x ON x.Menu_ID = ri.Menu_ID;

INSERT INTO dbo.ST_RightInfo (Menu_ID, Form_Name, Right_Values, Remark)
SELECT x.Menu_ID, m.Menu_No, x.Rights, N'BS卡务中心人员与组织权限位'
  FROM (
        SELECT Menu_ID, STUFF((SELECT '' + r.Right_ID
                                 FROM @CardRights r
                                WHERE r.Menu_ID = c.Menu_ID
                                ORDER BY r.Right_ID
                                FOR XML PATH('')), 1, 0, '') AS Rights
          FROM @CardRights c
         GROUP BY c.Menu_ID
       ) x
  JOIN dbo.ST_MenuInfo m ON m.Menu_ID = x.Menu_ID
 WHERE NOT EXISTS (SELECT 1 FROM dbo.ST_RightInfo ri WHERE ri.Menu_ID = x.Menu_ID);

/* 管理员角色默认拥有全部卡务权限位 */
UPDATE rr
   SET rr.Right_Values = ri.Right_Values
  FROM dbo.ST_RoleRight rr
  JOIN dbo.ST_RightInfo ri ON ri.Menu_ID = rr.Menu_ID
 WHERE rr.Role_No = 'Administrators'
   AND ri.Menu_ID IN (9001, 910101, 910102, 910103, 910104, 910105)
   AND ISNULL(rr.Right_Values, '') <> ri.Right_Values;

INSERT INTO dbo.ST_RoleRight (Role_No, Menu_ID, Right_Values)
SELECT 'Administrators', ri.Menu_ID, ri.Right_Values
  FROM dbo.ST_RightInfo ri
 WHERE ri.Menu_ID IN (9001, 910101, 910102, 910103, 910104, 910105)
   AND NOT EXISTS (SELECT 1 FROM dbo.ST_RoleRight rr WHERE rr.Role_No = 'Administrators' AND rr.Menu_ID = ri.Menu_ID);
GO

/*---------- 14.5 配置项 ----------*/
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'PersonNo.DefaultPrefix')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('PersonNo.DefaultPrefix', 'P', N'人员编号默认前缀，仅在库中无编号时使用');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'PersonNo.DefaultWidth')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('PersonNo.DefaultWidth', '6', N'人员编号数字位宽，仅在库中无编号时使用');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Dept.RootPrefix')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Dept.RootPrefix', '10', N'新增根部门编号前缀，留空则取现有最顶层编号前两位');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Face.DetectEnabled')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Face.DetectEnabled', '0', N'照片人脸检测开关：1 开启、0 关闭（关闭时仅做格式与尺寸校验）');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Face.ImportZipLimitMb')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Face.ImportZipLimitMb', '100', N'照片批量导入 ZIP 包大小上限（MB）');
GO

/*==============================================================================
  第 15 节  卡务中心 - 账户与卡
  依据《人脸通BS系统-卡务中心-账户与卡详细设计文档》
  - 只增不删：不修改、不重命名、不删除 CS 既有对象
  - 权威数据仍写 ST_Card / ST_CardType / ST_CardInfo / ST_LockCard 等 CS 表
  - 本模块只新增 BS_CardExt（扩展状态）与 BS_CardLog（动作审计）两张表
  - 脚本幂等，可重复执行
==============================================================================*/

/*---------- 15.1 新增表 ----------*/
IF OBJECT_ID('dbo.BS_CardExt', 'U') IS NULL
    CREATE TABLE dbo.BS_CardExt (
        Person_ID           INT           NOT NULL,
        Card_No             VARCHAR(12)   NULL,
        Last_Op_Type        VARCHAR(20)   NULL,
        Last_Op_Time        DATETIME      NULL,
        Last_Op_User        VARCHAR(20)   NULL,
        Lock_Reason_Code    VARCHAR(20)   NULL,
        Lock_Reason_Text    VARCHAR(100)  NULL,
        Lock_Time           DATETIME      NULL,
        Pwd_Fail_Count      INT           NOT NULL CONSTRAINT DF_BS_CardExt_Fail DEFAULT (0),
        Pwd_Lock_Until      DATETIME      NULL,
        Is_System_Frozen    BIT           NOT NULL CONSTRAINT DF_BS_CardExt_SysFrozen DEFAULT (0),
        Remark              VARCHAR(200)  NULL,
        Create_Time         DATETIME      NOT NULL CONSTRAINT DF_BS_CardExt_Create DEFAULT (GETDATE()),
        Update_Time         DATETIME      NULL,
        CONSTRAINT PK_BS_CardExt PRIMARY KEY CLUSTERED (Person_ID ASC)
    );
GO

IF OBJECT_ID('dbo.BS_CardLog', 'U') IS NULL
    CREATE TABLE dbo.BS_CardLog (
        ID_KEY          BIGINT IDENTITY(1,1) NOT NULL,
        Person_ID       INT           NOT NULL,
        Card_No         VARCHAR(12)   NULL,
        New_Card_No     VARCHAR(12)   NULL,
        Op_Type         VARCHAR(20)   NOT NULL,
        Before_State    TINYINT       NULL,
        After_State     TINYINT       NULL,
        Amount_Balance  MONEY         NULL,
        Amount_Small    MONEY         NULL,
        Amount_Foregift MONEY         NULL,
        Amount_Charge   MONEY         NULL,
        Refund_Amount   MONEY         NULL,
        Modify_Fee      MONEY         NULL,
        Request_No      VARCHAR(40)   NULL,
        Source          VARCHAR(20)   NULL,
        Op_User         VARCHAR(20)   NOT NULL,
        Op_Time         DATETIME      NOT NULL CONSTRAINT DF_BS_CardLog_Time DEFAULT (GETDATE()),
        Client_IP       VARCHAR(50)   NULL,
        Remark          VARCHAR(200)  NULL,
        Ext1            VARCHAR(100)  NULL,
        Ext2            VARCHAR(100)  NULL,
        CONSTRAINT PK_BS_CardLog PRIMARY KEY CLUSTERED (ID_KEY ASC)
    );
GO

/*---------- 15.2 索引补充 ----------*/
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_CardLog_Person' AND object_id = OBJECT_ID('dbo.BS_CardLog'))
    CREATE INDEX IX_BS_CardLog_Person ON dbo.BS_CardLog (Person_ID ASC, Op_Time DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_CardLog_Type' AND object_id = OBJECT_ID('dbo.BS_CardLog'))
    CREATE INDEX IX_BS_CardLog_Type ON dbo.BS_CardLog (Op_Type ASC, Op_Time DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_CardLog_Request' AND object_id = OBJECT_ID('dbo.BS_CardLog'))
    CREATE UNIQUE INDEX IX_BS_CardLog_Request ON dbo.BS_CardLog (Request_No) WHERE Request_No IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_CardLog_CardNo' AND object_id = OBJECT_ID('dbo.BS_CardLog'))
    CREATE INDEX IX_BS_CardLog_CardNo ON dbo.BS_CardLog (Card_No);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BS_CardExt_CardNo' AND object_id = OBJECT_ID('dbo.BS_CardExt'))
    CREATE INDEX IX_BS_CardExt_CardNo ON dbo.BS_CardExt (Card_No);
GO

/* CS 既有表上的索引属于只增操作，不改变 CS 的读写语义 */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ST_LockCard_Person_Date' AND object_id = OBJECT_ID('dbo.ST_LockCard'))
    CREATE INDEX IX_ST_LockCard_Person_Date ON dbo.ST_LockCard (Person_ID ASC, Lock_Date DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ST_UnLockCard_Person_Date' AND object_id = OBJECT_ID('dbo.ST_UnLockCard'))
    CREATE INDEX IX_ST_UnLockCard_Person_Date ON dbo.ST_UnLockCard (Person_ID ASC, UnLock_Date DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ST_AlterCard_Person_Date' AND object_id = OBJECT_ID('dbo.ST_AlterCard'))
    CREATE INDEX IX_ST_AlterCard_Person_Date ON dbo.ST_AlterCard (Person_ID ASC, Alter_Date DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ST_DeleteCard_Person_Date' AND object_id = OBJECT_ID('dbo.ST_DeleteCard'))
    CREATE INDEX IX_ST_DeleteCard_Person_Date ON dbo.ST_DeleteCard (Person_ID ASC, Del_Date DESC);
GO

/*---------- 15.3 卡务中心-账户与卡 菜单 ----------*/
DECLARE @CardAcctPages TABLE (
    Menu_ID INT NOT NULL,
    Menu_No VARCHAR(20) NOT NULL,
    Menu_Name NVARCHAR(20) NOT NULL,
    Form_Name VARCHAR(50) NOT NULL,
    Menu_Index TINYINT NOT NULL
);
INSERT INTO @CardAcctPages (Menu_ID, Menu_No, Menu_Name, Form_Name, Menu_Index) VALUES
    (910201, 'BS_CARD_ACCOUNT',     N'账户信息设置',   '/card/account',      1),
    (910202, 'BS_CARD_ACCOUNTTYPE', N'账户类别设置',   '/card/account-type', 2),
    (910203, 'BS_CARD_OPEN',        N'开户与销户',     '/card/account-open', 3),
    (910204, 'BS_CARD_FREEZE',      N'账户冻结与解冻', '/card/freeze',       4),
    (910205, 'BS_CARD_CHANGE',      N'换卡与补卡',     '/card/card-change',  5),
    (910206, 'BS_CARD_LOSS',        N'卡挂失与解挂',   '/card/card-loss',    6),
    (910207, 'BS_CARD_IDIMPORT',    N'导入授权 ID 卡', '/card/card-import',  7),
    (910208, 'BS_CARD_PERSONAL',    N'个人账户',       '/card/personal',     8);

INSERT INTO dbo.ST_MenuInfo (Menu_ID, System_No, Menu_No, Menu_Name, Para_ID, Call_Mode, Form_Name, Menu_Index)
SELECT p.Menu_ID, 'BS', p.Menu_No, p.Menu_Name, 9001, 1, p.Form_Name, p.Menu_Index
  FROM @CardAcctPages p
 WHERE NOT EXISTS (SELECT 1 FROM dbo.ST_MenuInfo m WHERE m.Menu_ID = p.Menu_ID);
GO

/*---------- 15.4 权限位与权限码映射 ----------*/
DECLARE @CardAcctRights TABLE (
    Menu_ID INT NOT NULL,
    Right_ID CHAR(1) NOT NULL,
    Right_Name NVARCHAR(20) NOT NULL,
    Permission_Code VARCHAR(64) NOT NULL,
    Sort_No TINYINT NOT NULL
);
INSERT INTO @CardAcctRights (Menu_ID, Right_ID, Right_Name, Permission_Code, Sort_No) VALUES
    (910201, '0', N'查看',     'card:account:view',          1),
    (910201, '1', N'保存',     'card:account:update',        2),
    (910202, '0', N'查看',     'card:accountType:view',      1),
    (910202, '1', N'新增',     'card:accountType:create',    2),
    (910202, '2', N'修改',     'card:accountType:update',    3),
    (910202, '3', N'停用',     'card:accountType:delete',    4),
    (910203, '0', N'查看',     'card:accountOpen:view',      1),
    (910203, '1', N'开户',     'card:accountOpen:open',      2),
    (910203, '2', N'修改卡',   'card:accountOpen:update',    3),
    (910203, '3', N'销户',     'card:accountOpen:close',     4),
    (910203, '4', N'销户复核', 'card:accountOpen:closeConfirm', 5),
    (910204, '0', N'查看',     'card:freeze:view',           1),
    (910204, '1', N'冻结',     'card:freeze:freeze',         2),
    (910204, '2', N'解冻',     'card:freeze:unfreeze',       3),
    (910205, '0', N'查看',     'card:cardChange:view',       1),
    (910205, '1', N'换卡补卡', 'card:cardChange:change',     2),
    (910206, '0', N'查看',     'card:cardLoss:view',         1),
    (910206, '1', N'挂失',     'card:cardLoss:loss',         2),
    (910206, '2', N'解挂',     'card:cardLoss:unloss',       3),
    (910207, '0', N'查看',     'card:cardImport:view',       1),
    (910207, '1', N'导入',     'card:cardImport:import',     2),
    (910207, '2', N'删除',     'card:cardImport:delete',     3),
    (910208, '0', N'查看',     'card:personal:view',         1),
    (910208, '1', N'改密',     'card:personal:changePwd',    2),
    (910208, '2', N'导出',     'card:personal:export',       3);

DECLARE @AcctMenuId INT, @AcctMenuNo VARCHAR(20);
DECLARE @AcctRightId CHAR(1), @AcctRightName NVARCHAR(20), @AcctCode VARCHAR(64);

DECLARE acct_right_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT Menu_ID, Right_ID, Right_Name, Permission_Code FROM @CardAcctRights ORDER BY Menu_ID, Sort_No;
OPEN acct_right_cursor;
FETCH NEXT FROM acct_right_cursor INTO @AcctMenuId, @AcctRightId, @AcctRightName, @AcctCode;
WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT @AcctMenuNo = Menu_No FROM dbo.ST_MenuInfo WHERE Menu_ID = @AcctMenuId;

    IF @AcctMenuNo IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.ST_RightValue WHERE Right_ID = @AcctRightId AND Form_Name = @AcctMenuNo)
            INSERT INTO dbo.ST_RightValue (Right_ID, Form_Name, Right_Name)
            VALUES (@AcctRightId, @AcctMenuNo, @AcctRightName);

        IF NOT EXISTS (SELECT 1 FROM dbo.BS_RightCodeMap WHERE Menu_ID = @AcctMenuId AND Right_ID = @AcctRightId)
            INSERT INTO dbo.BS_RightCodeMap (Menu_ID, Right_ID, Permission_Code, Api_Route, Remark)
            VALUES (@AcctMenuId, @AcctRightId, @AcctCode, NULL, N'BS卡务中心账户与卡权限码');
    END

    FETCH NEXT FROM acct_right_cursor INTO @AcctMenuId, @AcctRightId, @AcctRightName, @AcctCode;
END
CLOSE acct_right_cursor;
DEALLOCATE acct_right_cursor;

/* 汇总每个菜单的权限位字符串 */
UPDATE ri
   SET ri.Right_Values = x.Rights
  FROM dbo.ST_RightInfo ri
  JOIN (
        SELECT Menu_ID, STUFF((SELECT '' + r.Right_ID
                                 FROM @CardAcctRights r
                                WHERE r.Menu_ID = c.Menu_ID
                                ORDER BY r.Right_ID
                                FOR XML PATH('')), 1, 0, '') AS Rights
          FROM @CardAcctRights c
         GROUP BY c.Menu_ID
       ) x ON x.Menu_ID = ri.Menu_ID;

INSERT INTO dbo.ST_RightInfo (Menu_ID, Form_Name, Right_Values, Remark)
SELECT x.Menu_ID, m.Menu_No, x.Rights, N'BS卡务中心账户与卡权限位'
  FROM (
        SELECT Menu_ID, STUFF((SELECT '' + r.Right_ID
                                 FROM @CardAcctRights r
                                WHERE r.Menu_ID = c.Menu_ID
                                ORDER BY r.Right_ID
                                FOR XML PATH('')), 1, 0, '') AS Rights
          FROM @CardAcctRights c
         GROUP BY c.Menu_ID
       ) x
  JOIN dbo.ST_MenuInfo m ON m.Menu_ID = x.Menu_ID
 WHERE NOT EXISTS (SELECT 1 FROM dbo.ST_RightInfo ri WHERE ri.Menu_ID = x.Menu_ID);

/* 管理员角色默认拥有全部账户与卡权限位 */
UPDATE rr
   SET rr.Right_Values = ri.Right_Values
  FROM dbo.ST_RoleRight rr
  JOIN dbo.ST_RightInfo ri ON ri.Menu_ID = rr.Menu_ID
 WHERE rr.Role_No = 'Administrators'
   AND ri.Menu_ID IN (910201, 910202, 910203, 910204, 910205, 910206, 910207, 910208)
   AND ISNULL(rr.Right_Values, '') <> ri.Right_Values;

INSERT INTO dbo.ST_RoleRight (Role_No, Menu_ID, Right_Values)
SELECT 'Administrators', ri.Menu_ID, ri.Right_Values
  FROM dbo.ST_RightInfo ri
 WHERE ri.Menu_ID IN (910201, 910202, 910203, 910204, 910205, 910206, 910207, 910208)
   AND NOT EXISTS (SELECT 1 FROM dbo.ST_RoleRight rr WHERE rr.Role_No = 'Administrators' AND rr.Menu_ID = ri.Menu_ID);
GO

/*---------- 15.5 配置项 ----------*/
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Card.Open.SupplyRemark')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Card.Open.SupplyRemark', N'开户充值', N'开户时写入 ST_SupplyFund.Remark 的固定文案，与 CS 保持可配置一致');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Card.Change.BlackRemark')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Card.Change.BlackRemark', N'换卡产生', N'换卡后旧卡号写入 ST_BlackPerson 的备注');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Card.Close.BlackRemark')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Card.Close.BlackRemark', N'销卡产生', N'销户后卡号写入 ST_BlackPerson 的备注');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Card.PwdVerify.Mode')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Card.PwdVerify.Mode', 'Card', N'卡密码校验模式：Card 必须校验卡密码；Manager 仅需复核码；Both 二者其一');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Card.Pwd.SyncCard')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Card.Pwd.SyncCard', '1', N'改密是否必须写卡成功后才写库：1 必须、0 允许仅写库（标注卡内未同步）');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Card.Pwd.AllowView')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Card.Pwd.AllowView', '0', N'是否允许查看卡密码明文：1 允许（需复核码并写审计）、0 关闭');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Card.Pwd.MaxFail')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Card.Pwd.MaxFail', '5', N'卡密码连续失败锁定阈值，达到后锁定 Card.Pwd.LockMinutes 分钟');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Card.Pwd.LockMinutes')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Card.Pwd.LockMinutes', '10', N'卡密码失败锁定时长（分钟），仅影响 BS 操作');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Card.Loss.RemindDays')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Card.Loss.RemindDays', '30', N'挂失超过该天数进入长期挂失清单');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Card.Loss.AutoCloseDays')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Card.Loss.AutoCloseDays', '0', N'超期挂失转入建议销户清单的天数，0 表示不处理');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Card.Close.CheckInflight')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Card.Close.CheckInflight', '1', N'销户是否校验在途交易：1 校验、0 关闭');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Card.Open.CheckBlacklist')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Card.Open.CheckBlacklist', '1', N'开户是否校验卡号黑名单：1 命中时提示二次确认、0 不校验');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'SuperPwd.Enabled')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('SuperPwd.Enabled', '1', N'是否启用 CS 超级密码（ST_ONECARD 或 Admin 密码）校验');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Card.Freeze.BatchLimit')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Card.Freeze.BatchLimit', '200', N'批量冻结单次上限（条）');
IF NOT EXISTS (SELECT 1 FROM dbo.BS_SysConfig WHERE Config_Key = 'Card.IdCard.BatchDeleteLimit')
    INSERT INTO dbo.BS_SysConfig (Config_Key, Config_Value, Remark)
    VALUES ('Card.IdCard.BatchDeleteLimit', '100', N'授权 ID 卡批量删除单次上限（条）');
GO
