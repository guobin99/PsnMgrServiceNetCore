
/*=============================================================================
  文件: KQ_CalcDaily.sql   (SQL Server 2005)
  功能: 日考勤统计存储过程(移植自 Delphi uDayRep.pas 的计算逻辑)
  说明:
    1. 主过程: dbo.usp_KQ_CalcDaily  @DateBgn, @DateEnd, @PersonList
    2. 刷卡匹配: dbo.usp_KQ_MatchBrush (由主过程调用,不单独使用)
    3. 辅助函数: udf_KQ_Min / udf_KQ_ToDT / udf_KQ_FactHour / udf_KQ_Fmt
    4. 班次类型: A=正常班  B=规律班(倒班)  C=灵活班  其他=自定义班
    5. 考勤方式(Worktime_Mode): 1=两次刷卡 2=一天只刷一次卡
                              3=只统计刷卡时间 4=无刷卡计算工时
    6. 班次段信息取 KQ_Class 的 Sect1..Sect5 共 5 段
    7. 注意: 异常原因文字(旷工/请假/迟到/早退/忘刷卡/调休补班/调休/补考勤)
             请与 uConstKQ.pas 中常量保持一致(存储过程中已集中定义为变量)
EXEC dbo.usp_KQ_CalcDaily '2026-10-01', '2026-10-31', ''              -- 全部人员
EXEC dbo.usp_KQ_CalcDaily '2026-10-01', '2026-10-31', '101,102,205'   -- 指定人员
=============================================================================*/
/*---------------------------------------------------------------------------*/
/* 工具函数: 计算两个时间相差的分钟数(a-b,带符号),对应 fnMinutesBetween        */
/*---------------------------------------------------------------------------*/
IF OBJECT_ID('dbo.udf_KQ_Min') IS NOT NULL DROP FUNCTION dbo.udf_KQ_Min
GO
CREATE FUNCTION dbo.udf_KQ_Min(@a datetime, @b datetime)
RETURNS int
AS
BEGIN
    RETURN DATEDIFF(minute, @b, @a)
END
GO

/*-----------������������----------------------------------------------------*/
/* 工具函数: 把 'HH:MM' 班次时间转为日期时间(支持 >=24 点的跨天时间)          */
/*           对应 fnTo24Hour; 时间为空返回 NULL                              */
/*---------------------------------------------------------------------------*/
IF OBJECT_ID('dbo.udf_KQ_ToDT') IS NOT NULL DROP FUNCTION dbo.udf_KQ_ToDT
GO
CREATE FUNCTION dbo.udf_KQ_ToDT(@tm varchar(5), @dt datetime)
RETURNS datetime
AS
BEGIN
    IF ISNULL(@tm,'') = '' RETURN NULL
    DECLARE @h int, @m int, @add int
    SET @h = CAST(LEFT(@tm,2) AS int)
    SET @m = CAST(RIGHT(@tm,2) AS int)
    SET @add = 0
    IF @h >= 24 BEGIN SET @h = @h - 24 SET @add = 1 END
    RETURN DATEADD(minute, @h*60 + @m, DATEADD(day, @add, @dt))
END
GO

/*-----------������������----------------------------------------------------*/
/* 工具函数: 一次只统计一个时段的实际工时,对应 fnGetFactWorkHour              */
/*---------------------------------------------------------------------------*/
IF OBJECT_ID('dbo.udf_KQ_FactHour') IS NOT NULL DROP FUNCTION dbo.udf_KQ_FactHour
GO
CREATE FUNCTION dbo.udf_KQ_FactHour(@must decimal(18,4), @fact decimal(18,4), @deduct int)
RETURNS decimal(18,4)
AS
BEGIN
    IF @fact > @must * 0.5
    BEGIN
        IF @fact < @must * 0.5 + @deduct + 1
            RETURN ROUND(@must * 0.5, 2)
        ELSE
            RETURN ROUND(@fact - @deduct, 2)
    END
    RETURN ROUND(@fact, 2)
END
GO

/*-----------������������----------------------------------------------------*/
/* 工具函数: 分钟数按参数格式转成小时,对应 fnFormatData                        */
/*   @MinToHour=1: 按 OneHour/HalfHour 取整(如 0.5 小时进制)                */
/*   @MinToHour=0: 直接除以 60,保留 @Dec 位小数                             */
/*---------------------------------------------------------------------------*/
IF OBJECT_ID('dbo.udf_KQ_Fmt') IS NOT NULL DROP FUNCTION dbo.udf_KQ_Fmt
GO
CREATE FUNCTION dbo.udf_KQ_Fmt(@mins decimal(18,4), @MinToHour bit,
                               @OneHour int, @HalfHour int, @Dec int)
RETURNS decimal(18,4)
AS
BEGIN
    IF @MinToHour = 1
    BEGIN
        DECLARE @i int, @r decimal(18,4)
        SET @i = FLOOR(@mins / 60)
        SET @r = @mins - @i * 60
        RETURN @i + CASE WHEN @r >= @OneHour THEN 1
                         WHEN @r >= @HalfHour THEN 0.5 ELSE 0 END
    END
    RETURN ROUND(@mins / 60.0, @Dec)
END
GO

/*=============================================================================
  存储过程: dbo.usp_KQ_MatchBrush
  功能   : 对某员工某天按班次 5 个时段匹配刷卡并自动填卡
           (移植 fnMatchBrushInCard / fnMathcBrushOutCard / fnAutoFillCard)
  参数   : @PersonID     员工标识
           @Date         工作日
           @ClassNo      班别编号
           @Mode         考勤方式(1,2,3,4)
           @Late3E       迟到最大分钟数(参数 late3e)
           @Early3E      早退最大分钟数(参数 early3e)
           @OTBig        计为加班的分钟数(参数 overtimebig)
           @OutAddMinute 下班取卡偏移(参数 voutaddminute,默认30)
           @StartSeq     传入: 刷卡记录起始序号(员工内跨日延续)
           @EndSeq       输出: 匹配结束后的序号
           @BrushData    输出: 当日有效刷卡串(如 '08:30,17:45')
           @Result       输出: 1=找到班次 0=班次不存在
  依赖   : 调用方须先创建 #Sect 表(结构见主过程)
=============================================================================*/
IF OBJECT_ID('dbo.usp_KQ_MatchBrush') IS NOT NULL DROP PROC dbo.usp_KQ_MatchBrush
GO
CREATE PROC dbo.usp_KQ_MatchBrush
    @PersonID     int,
    @Date         datetime,
    @ClassNo      varchar(20),
    @Mode         int,
    @Late3E       int,
    @Early3E      int,
    @OTBig        int,
    @OutAddMinute int,
    @StartSeq     int,
    @EndSeq       int          OUTPUT,
    @BrushData    varchar(255) OUTPUT,
    @Result       int          OUTPUT
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @i int, @cnt int, @bt datetime, @fl int, @done bit
    DECLARE @k int, @j int
    DECLARE @sInDt datetime, @sOutDt datetime, @sInBgn datetime, @sInEnd datetime
    DECLARE @sOutBgn datetime, @sOutEnd datetime
    DECLARE @sInCard int, @sOutCard int, @sOT bit, @sDeduct int
    DECLARE @ldOut datetime, @cardState int

    /*------------------ 1. 装载班次 5 段信息到 #Sect ------------------*/
    DELETE #Sect;

	WITH s(n) AS (SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3
                   UNION ALL SELECT 4 UNION ALL SELECT 5)
    INSERT #Sect (SectNo, TimeIn, TimeOut, Ahead, After, InCard, OutCard, Deduct, OT)
    SELECT s.n,
           CASE s.n WHEN 1 THEN c.Sect1_InTime  WHEN 2 THEN c.Sect2_InTime
                    WHEN 3 THEN c.Sect3_InTime  WHEN 4 THEN c.Sect4_InTime
                    ELSE c.Sect5_InTime END,
           CASE s.n WHEN 1 THEN c.Sect1_OutTime WHEN 2 THEN c.Sect2_OutTime
                    WHEN 3 THEN c.Sect3_OutTime WHEN 4 THEN c.Sect4_OutTime
                    ELSE c.Sect5_OutTime END,
           CASE s.n WHEN 1 THEN c.Sect1_Ahead  WHEN 2 THEN c.Sect2_Ahead
                    WHEN 3 THEN c.Sect3_Ahead  WHEN 4 THEN c.Sect4_Ahead
                    ELSE c.Sect5_Ahead END,
           CASE s.n WHEN 1 THEN c.Sect1_After  WHEN 2 THEN c.Sect2_After
                    WHEN 3 THEN c.Sect3_After  WHEN 4 THEN c.Sect4_After
                    ELSE c.Sect5_After END,
           CASE s.n WHEN 1 THEN c.Sect1_InCard WHEN 2 THEN c.Sect2_InCard
                    WHEN 3 THEN c.Sect3_InCard WHEN 4 THEN c.Sect4_InCard
                    ELSE c.Sect5_InCard END,
           CASE s.n WHEN 1 THEN c.Sect1_OutCard WHEN 2 THEN c.Sect2_OutCard
                    WHEN 3 THEN c.Sect3_OutCard WHEN 4 THEN c.Sect4_OutCard
                    ELSE c.Sect5_OutCard END,
           CASE s.n WHEN 1 THEN c.Sect1_Deduct WHEN 2 THEN c.Sect2_Deduct
                    WHEN 3 THEN c.Sect3_Deduct WHEN 4 THEN c.Sect4_Deduct
                    ELSE c.Sect5_Deduct END,
           CASE s.n WHEN 1 THEN c.Sect1_Overtime WHEN 2 THEN c.Sect2_Overtime
                    WHEN 3 THEN c.Sect3_Overtime WHEN 4 THEN c.Sect4_Overtime
                    ELSE c.Sect5_Overtime END
    FROM KQ_Class c CROSS JOIN s
    WHERE c.Class_No = @ClassNo

    IF @@ROWCOUNT = 0 BEGIN SET @Result = 0 RETURN END

    /* 计算各段日期时间范围(与 fnMatchBrushCard 中一致) */
    UPDATE #Sect SET
        InDt     = dbo.udf_KQ_ToDT(TimeIn,  @Date),
        OutDt    = dbo.udf_KQ_ToDT(TimeOut, @Date),
        InBgn    = DATEADD(minute, -Ahead, dbo.udf_KQ_ToDT(TimeIn,  @Date)),
        InEnd    = DATEADD(minute, @Late3E, dbo.udf_KQ_ToDT(TimeIn, @Date)),
        OutBgn   = DATEADD(minute, -@Early3E, dbo.udf_KQ_ToDT(TimeOut, @Date)),
        OutEnd   = DATEADD(minute, After,  dbo.udf_KQ_ToDT(TimeOut, @Date))
    /* 加班段: 有效上班结束时间 = 下班时间 - OvertimeBig,且不小于 OutBgn */
    UPDATE #Sect SET InEnd = CASE WHEN DATEADD(minute,-@OTBig,OutDt) > OutBgn
                                  THEN DATEADD(minute,-@OTBig,OutDt) ELSE OutBgn END
    WHERE OT = 1
	--select * from #BC
    /*------------------ 2. 装载该员工前后一天刷卡记录 ------------------*/
    DECLARE @Cards TABLE (seq int IDENTITY(1,1) PRIMARY KEY, bt datetime, fl int)
    INSERT @Cards (bt, fl)
    /*SELECT CASE WHEN CAST(LEFT(Brush_Time,2) AS int) >= 24 THEN DATEADD(minute, CAST(LEFT(Brush_Time,2) AS int)*60  + CAST(SUBSTRING(Brush_Time,4,2) AS int) - 1440, CONVERT(datetime,Brush_Date))
                ELSE DATEADD(minute, CAST(LEFT(Brush_Time,2) AS int)*60  + CAST(SUBSTRING(Brush_Time,4,2) AS int),  CONVERT(datetime,Brush_Date)) END,  Data_Flag)）*/
    select  bt,fl
    FROM #BC
    WHERE Person_ID = @PersonID
      AND bt >= DATEADD(day,-1,@Date) AND bt < DATEADD(day,2,@Date)
    ORDER BY bt
    SELECT @cnt = COUNT(*) FROM @Cards
    --SET @i = @StartSeq
	SET @i = 1

	--select @StartSeq,* from @Cards

    /*------------------ 3. 逐段匹配刷卡(对应 fnMatchBrushCard) ------------------*/
    SET @k = 1
    WHILE @k <= 5
    BEGIN
        SELECT @sInDt=InDt, @sOutDt=OutDt, @sInBgn=InBgn, @sInEnd=InEnd,
               @sOutBgn=OutBgn, @sOutEnd=OutEnd, @sInCard=InCard,
               @sOutCard=OutCard, @sOT=OT
        FROM #Sect WHERE SectNo = @k


        /* 上下班时间为空则停止(方式3"只统计刷卡时间"除外) */
        IF (@Mode <> 3) AND (@sInDt IS NULL OR @sOutDt IS NULL) BREAK
        IF @Mode = 4 BREAK  /* 方式4"无刷卡计算工时"不匹配 */

        IF @Mode = 3
        BEGIN
            /* 方式3: 直接用第1段的有效范围,范围内第一张卡作为上班卡(跳过外出/出差12,14) */
            SELECT @sInBgn = InBgn, @sOutEnd = OutEnd FROM #Sect WHERE SectNo = 1
            SET @done = 0
            WHILE @i <= @cnt AND @done = 0
            BEGIN
                SELECT @bt = bt, @fl = fl FROM @Cards WHERE seq = @i
                IF @fl NOT IN (12,14) AND @bt BETWEEN @sInBgn AND @sOutEnd
                BEGIN
                    UPDATE #Sect SET InFact = @bt WHERE SectNo = @k
                    SET @i = @i + 1 SET @done = 1
                END
                IF @done = 0 AND @bt >= @sOutBgn SET @done = 1
                IF @done = 0 SET @i = @i + 1
            END
            /* 范围内下一张卡作为下班卡 */
            SET @done = 0
            WHILE @i <= @cnt AND @done = 0
            BEGIN
                SELECT @bt = bt, @fl = fl FROM @Cards WHERE seq = @i
                IF @bt BETWEEN @sInBgn AND @sOutEnd
                BEGIN
                    UPDATE #Sect SET OutFact = @bt WHERE SectNo = @k
                    SET @i = @i + 1 SET @done = 1
                END
                IF @done = 0 AND @bt > @sOutEnd SET @done = 1
                IF @done = 0 SET @i = @i + 1
            END
            SET @k = @k + 1
            CONTINUE
        END

        /*--- 匹配上班卡 (fnMatchBrushInCard) ---*/
        SET @done = 0
        WHILE @i <= @cnt AND @done = 0
        BEGIN
            SELECT @bt = bt, @fl = fl FROM @Cards WHERE seq = @i
            IF @fl NOT IN (12,14)   /* 跳过外出/出差记录 */
            BEGIN
                IF @bt BETWEEN @sInBgn AND @sInEnd
                BEGIN
                    IF @k <> 1 AND @sOT = 1 AND @sInCard = 2
                       AND DATEDIFF(minute,@sInDt,@bt) >= @OTBig
                        SET @done = 1   /* 加班段按下班刷卡计时,退出 */
                    ELSE IF @sInCard = 1 OR (@sInCard = 2
                            AND DATEDIFF(minute,@sInDt,@bt) <= @Late3E)
                    BEGIN
                        UPDATE #Sect SET InFact = @bt WHERE SectNo = @k
                        SET @i = @i + 1 SET @done = 1
                    END
                    ELSE SET @done = 1
                END
            END
            IF @done = 0 AND @bt >= @sOutBgn SET @done = 1
            IF @done = 0 SET @i = @i + 1
        END

        /*--- 匹配下班卡 (fnMathcBrushOutCard,含 Caizf 2013 修改) ---*/
        SET @ldOut = NULL SET @done = 0
        WHILE @i <= @cnt AND @done = 0
        BEGIN
            SELECT @bt = bt, @fl = fl FROM @Cards WHERE seq = @i
            IF @bt > @sOutEnd SET @done = 1
            ELSE
            BEGIN
                IF @fl NOT IN (11,13)   /* 跳过出差/外出记录 */
                BEGIN
                    IF @sOT = 0
                    BEGIN
                        IF @bt >= @sOutDt
                        BEGIN
                            IF @ldOut IS NULL OR @ldOut < @sOutBgn
                               OR DATEDIFF(minute,@ldOut,@sOutDt)
                                  >= DATEDIFF(minute,@sOutDt,@bt) - @OutAddMinute
                            BEGIN SET @ldOut = @bt SET @i = @i + 1 END
                            SET @done = 1
                        END
                        ELSE IF DATEDIFF(minute,@sInDt,@bt) > @Late3E
                            SET @ldOut = @bt
                    END
                    ELSE IF DATEDIFF(minute,@sInDt,@bt) > 0
                    BEGIN
                        SET @ldOut = @bt
                        IF DATEDIFF(minute,@sInDt,@bt) >= @OTBig
                        BEGIN SET @i = @i + 1 SET @done = 1 END
                    END
                END
                IF @done = 0 SET @i = @i + 1
            END
        END
        IF @ldOut IS NOT NULL
        BEGIN
            IF @sOT = 1 AND @sInCard = 1
               AND (SELECT InFact FROM #Sect WHERE SectNo = @k) IS NULL
            BEGIN IF @i > 0 SET @i = @i - 1 END
            ELSE
                UPDATE #Sect SET OutFact = @ldOut WHERE SectNo = @k
        END
        SET @k = @k + 1
    END

    /*------------------ 4. 自动填"必"/"可"卡 (fnAutoFillCard) ------------------*/
    IF @Mode <> 4
    BEGIN
        SET @cardState = 0
        SET @k = 5
        WHILE @k >= 1
        BEGIN
            SELECT @sInDt=InDt, @sOutDt=OutDt, @sInCard=InCard, @sOutCard=OutCard
            FROM #Sect WHERE SectNo = @k
            /* 自动填下班卡 */
            IF @sOutDt IS NOT NULL
            BEGIN
                IF (SELECT OutFact FROM #Sect WHERE SectNo = @k) IS NOT NULL
                    SET @cardState = 2
                ELSE IF @sOutCard = 0
                BEGIN
                    IF @sInCard = 0 OR (SELECT InFact FROM #Sect WHERE SectNo=@k) IS NOT NULL
                       OR @cardState > 0
                    BEGIN
                        UPDATE #Sect SET OutFact = @sOutDt WHERE SectNo = @k
                        SET @cardState = 2
                    END
                END
                ELSE IF @sOutCard = 2 AND @cardState > 0
                BEGIN
                    SET @j = @k
                    WHILE @j >= 1
                    BEGIN
                        IF (SELECT InFact FROM #Sect WHERE SectNo=@j) IS NOT NULL
                           OR (@j > 1 AND (SELECT OutFact FROM #Sect WHERE SectNo=@j-1) IS NOT NULL)
                        BEGIN
                            UPDATE #Sect SET OutFact = @sOutDt WHERE SectNo = @k
                            BREAK
                        END
                        SET @j = @j - 1
                    END
                END
            END
            /* 自动填上班卡 */
            IF @sInDt IS NOT NULL
            BEGIN
                IF (SELECT InFact FROM #Sect WHERE SectNo = @k) IS NOT NULL
                    SET @cardState = 1
                ELSE IF @sInCard <> 1
                     AND (SELECT OutFact FROM #Sect WHERE SectNo = @k) IS NOT NULL
                BEGIN
                    UPDATE #Sect SET InFact = @sInDt WHERE SectNo = @k
                    SET @cardState = 1
                END
            END
            SET @k = @k - 1
        END
    END

    /*------------------ 5. 生成当日有效刷卡串 (fnGetBrushData) ------------------*/
    SET @BrushData = ''
    SELECT @BrushData = COALESCE(@BrushData + ',','')
        + CASE WHEN DATEDIFF(day,@Date,bt) = 0
               THEN RIGHT('0'+CAST(DATEPART(hh,bt) AS varchar),2)+':'+RIGHT('0'+CAST(DATEPART(mi,bt) AS varchar),2)
               ELSE CAST(DATEPART(hh,bt)+24*DATEDIFF(day,@Date,bt) AS varchar)+':'+RIGHT('0'+CAST(DATEPART(mi,bt) AS varchar),2)
          END
    FROM @Cards c
    WHERE EXISTS (SELECT 1 FROM #Sect s WHERE c.bt BETWEEN s.InBgn AND s.OutEnd)
    ORDER BY c.bt

    SET @EndSeq = @i
    SET @Result = 1

	
END
GO

/*-----------������������----------------------------------------------------*/
/* 工具函数: 按规律班(倒班)规则取某日班次,对应 fnSetRuleClassInfo+fnGetDateClassNo */
/*   规则方式 Rule_Mode: 0=按天倒班 2=按月内天数倒班 其他=按月倒班            */
/*---------------------------------------------------------------------------*/
IF OBJECT_ID('dbo.udf_KQ_RuleClass') IS NOT NULL DROP FUNCTION dbo.udf_KQ_RuleClass
GO
CREATE FUNCTION dbo.udf_KQ_RuleClass(@RuleClassNo varchar(20), @Date datetime)
RETURNS varchar(20)
AS
BEGIN
    DECLARE @Mode int, @Cycle int, @Start datetime
    SELECT @Mode = Rule_Mode, @Cycle = Rule_Cycle, @Start = Start_Date
    FROM KQ_RuleClass WHERE Class_No = @RuleClassNo
    IF @Cycle IS NULL OR @Cycle <= 0 RETURN NULL

    DECLARE @t TABLE (id int IDENTITY(1,1), Shift_ClassNo varchar(20), Shift_Days int)
    INSERT @t (Shift_ClassNo, Shift_Days)
    SELECT Shift_ClassNo, Shift_Days FROM KQ_RuleClassSub
    WHERE Class_No = @RuleClassNo ORDER BY ID_KEY
    IF @@ROWCOUNT = 0 RETURN NULL

    DECLARE @mod int
    SET @mod = CASE @Mode WHEN 0 THEN DATEDIFF(day,@Start,@Date)
                          WHEN 2 THEN DAY(@Date) - 1
                          ELSE MONTH(@Date) - MONTH(@Start) END
    SET @mod = @mod % @Cycle
    IF @mod < 0 SET @mod = @mod + @Cycle

    DECLARE @i int, @cnt int, @acc int, @c varchar(20), @d int
    SELECT @cnt = COUNT(*) FROM @t
    SET @i = 1 SET @acc = 0
    WHILE @i <= @cnt
    BEGIN
        SELECT @c = Shift_ClassNo, @d = Shift_Days FROM @t WHERE id = @i
        SET @acc = @acc + @d
        IF @mod < @acc RETURN @c
        SET @i = @i + 1
    END
    RETURN NULL
END
GO

/*=============================================================================
  存储过程: dbo.usp_KQ_CalcDaily
  功能   : 统计人员日考勤数据(移植 uDayRep.pas 的日报统计逻辑)
  参数   : @DateBgn    开始日期 'yyyy-mm-dd'
           @DateEnd    截止日期 'yyyy-mm-dd'
           @PersonList 人员ID列表(逗号分隔,空=全部 KQ_Person 人员)
  流程   : 1.读取考勤参数 2.收集原始刷卡+补刷卡 3.逐人逐日:
             定班次(调班>规律班/灵活班/自定义班) -> 匹配刷卡 ->
             计算工时/迟到早退/缺勤 -> 请假/调休/加班等异常 ->
             写入 #Daily 4.事务内覆盖写回 KQ_Daily
EXEC dbo.usp_KQ_CalcDaily '2026-10-01', '2026-10-31', ''              -- 全部人员
EXEC dbo.usp_KQ_CalcDaily '2026-10-01', '2026-10-31', '101,102,205'   -- 指定人员
=============================================================================*/
IF OBJECT_ID('dbo.usp_KQ_CalcDaily') IS NOT NULL DROP PROC dbo.usp_KQ_CalcDaily
GO
CREATE PROC dbo.usp_KQ_CalcDaily
    @DateBgn    varchar(10),
    @DateEnd    varchar(10),
    @PersonList varchar(8000) = ''
AS
BEGIN
    SET NOCOUNT ON

    /*---------------- 0. 常量(与 uConstKQ.pas 保持一致) ----------------*/
    DECLARE @S_Absent varchar(20)     SET @S_Absent = '旷工'      -- SExceptionAbsent
    DECLARE @S_Leave varchar(20)      SET @S_Leave = '请假'       -- SExceptionLeave
    DECLARE @S_Late varchar(20)       SET @S_Late = '迟到'        -- SExceptionLate
    DECLARE @S_Early varchar(20)      SET @S_Early = '早退'       -- SExceptionEarly
    DECLARE @S_Forget varchar(20)     SET @S_Forget = '漏刷'    -- SExceptionForget
    DECLARE @S_FillClass varchar(20)  SET @S_FillClass = '补班' -- SExceptionFillClass
    DECLARE @S_FillAttend varchar(20) SET @S_FillAttend = '补出勤'  -- SExceptionFillAttend
    DECLARE @S_AdjustRest varchar(20) SET @S_AdjustRest = '调休'    -- SExceptionAdjustRest

    /*---------------- 1. 读取考勤参数 (fnInitAttendParam) ----------------*/
    DECLARE @Late1S int, @Late1E int, @Late2E int, @Late3E int
    DECLARE @Early1S int, @Early1E int, @Early2E int, @Early3E int
    DECLARE @IsDeducte bit, @MinToHour bit, @OneHour int, @HalfHour int, @Dec int
    DECLARE @LeaveDatum int, @OTDatum int, @OTBig int, @IsDeductLE bit, @OutAddMinute int
    DECLARE @bAudit bit

    SELECT @Late1S  = COALESCE((SELECT CAST(Param_Value AS int) FROM KQ_SysParam WHERE LOWER(Param_Code)='late1s'),0),
           @Late1E  = COALESCE((SELECT CAST(Param_Value AS int) FROM KQ_SysParam WHERE LOWER(Param_Code)='late1e'),0),
           @Late2E  = COALESCE((SELECT CAST(Param_Value AS int) FROM KQ_SysParam WHERE LOWER(Param_Code)='late2e'),0),
           @Late3E  = COALESCE((SELECT CAST(Param_Value AS int) FROM KQ_SysParam WHERE LOWER(Param_Code)='late3e'),0),
           @Early1S = COALESCE((SELECT CAST(Param_Value AS int) FROM KQ_SysParam WHERE LOWER(Param_Code)='early1s'),0),
           @Early1E = COALESCE((SELECT CAST(Param_Value AS int) FROM KQ_SysParam WHERE LOWER(Param_Code)='early1e'),0),
           @Early2E = COALESCE((SELECT CAST(Param_Value AS int) FROM KQ_SysParam WHERE LOWER(Param_Code)='early2e'),0),
           @Early3E = COALESCE((SELECT CAST(Param_Value AS int) FROM KQ_SysParam WHERE LOWER(Param_Code)='early3e'),0),
           @IsDeducte = COALESCE((SELECT CAST(Param_Value AS int) FROM KQ_SysParam WHERE LOWER(Param_Code)='is_deducte'),0),
           @IsDeductLE = COALESCE((SELECT CAST(Param_Value AS int) FROM KQ_SysParam WHERE LOWER(Param_Code)='isdeductle'),0),
           @MinToHour = CASE COALESCE((SELECT Param_Value FROM KQ_SysParam WHERE LOWER(Param_Code)='mintohour'),'1')
                         WHEN '0' THEN 1 ELSE 0 END,   -- mintohour=0 表示"分钟要转成小时"
           @OneHour = COALESCE((SELECT CAST(Param_Value AS int) FROM KQ_SysParam WHERE LOWER(Param_Code)='onehour'),60),
           @HalfHour = COALESCE((SELECT CAST(Param_Value AS int) FROM KQ_SysParam WHERE LOWER(Param_Code)='halfhour'),30),
           @Dec = COALESCE((SELECT CAST(Param_Value AS int) FROM KQ_SysParam WHERE LOWER(Param_Code)='decimal'),2),
           @LeaveDatum = COALESCE((SELECT CAST(Param_Value AS int) FROM KQ_SysParam WHERE LOWER(Param_Code)='leave_standard'),0),
           @OTDatum = COALESCE((SELECT CAST(Param_Value AS int) FROM KQ_SysParam WHERE LOWER(Param_Code)='overtime_standard'),0),
           @OTBig = COALESCE((SELECT CAST(Param_Value AS int) FROM KQ_SysParam WHERE LOWER(Param_Code)='overtimebig'),0),
           @OutAddMinute = COALESCE((SELECT CAST(Param_Value AS int) FROM KQ_SysParam WHERE LOWER(Param_Code)='voutaddminute'),30),
           @bAudit = COALESCE((SELECT CAST(Param_Value AS int) FROM KQ_SysParam WHERE Param_Code='Is_Auditing'),0)

    /*---------------- 2. 临时表 ----------------*/
    CREATE TABLE #Person  (Person_ID int PRIMARY KEY)
    CREATE TABLE #BC      (Person_ID int, bt datetime, fl int)
    CREATE INDEX IX_BC ON #BC(Person_ID, bt)
    CREATE TABLE #BCP     (seq int PRIMARY KEY, bt datetime, fl int)  -- 当前员工刷卡(有序)
    CREATE TABLE #Daily   (Person_ID int, Worktime_Date varchar(10), OfWeek varchar(10),
        Class_No varchar(20), Class_Data varchar(60),
        Sect1_InFact varchar(16), Sect1_OutFact varchar(16), Sect2_InFact varchar(16),
        Sect2_OutFact varchar(16), Sect3_InFact varchar(16), Sect3_OutFact varchar(16),
        Sect4_InFact varchar(16), Sect4_OutFact varchar(16), Sect5_InFact varchar(16),
        Sect5_OutFact varchar(16),
        Must_Work decimal(18,2), Fact_Work decimal(18,2), Absent_Work decimal(18,2),
        Reason varchar(40), Leave_Hour decimal(18,2), Leave_Data varchar(60),
        Leave_Name varchar(40), Overtime_Hour decimal(18,2), Overtime_Class char(1),
        Late_Minute int, Late_Data varchar(20), Early_Minute int, Early_Data varchar(20),
        Brush_Data varchar(255))
    CREATE TABLE #Holiday (Holiday varchar(5), Holiday_Name varchar(50))
    CREATE TABLE #L   (Person_ID int, Leave_Date varchar(10), Leave_Hour decimal(18,2),
        Kind_No varchar(10), Kind_Name varchar(50), Is_Deduct bit, IsOutLeave bit,
        rid int IDENTITY(1,1))
    CREATE INDEX IX_L ON #L(Person_ID, Leave_Date)
    CREATE TABLE #AC  (Person_ID int, Adjust_Date varchar(10), Class_No varchar(20))
    CREATE TABLE #AR  (Person_ID int, Adjust_Date varchar(10), Adjust_Class int, Adjust_Hour decimal(18,2))
    CREATE TABLE #OT  (Person_ID int, Over_Date varchar(10), Over_Hour decimal(18,2))
    CREATE TABLE #FA  (Person_ID int, Fill_Date varchar(10), Fill_Class int, Fill_Hour decimal(18,2))
    CREATE INDEX IX_AC ON #AC(Person_ID, Adjust_Date)
    CREATE INDEX IX_AR ON #AR(Person_ID, Adjust_Date, Adjust_Class)
    CREATE INDEX IX_OT ON #OT(Person_ID, Over_Date)
    CREATE INDEX IX_FA ON #FA(Person_ID, Fill_Date, Fill_Class)
    CREATE TABLE #Sect (SectNo int PRIMARY KEY, TimeIn varchar(5), TimeOut varchar(5),
        Ahead int, After int, InCard int, OutCard int, Deduct int, OT bit,
        InDt datetime, OutDt datetime, InBgn datetime, InEnd datetime,
        OutBgn datetime, OutEnd datetime, InFact datetime, OutFact datetime)
    CREATE TABLE #SectBest (SectNo int PRIMARY KEY, TimeIn varchar(5), TimeOut varchar(5),
        Ahead int, After int, InCard int, OutCard int, Deduct int, OT bit,
        InDt datetime, OutDt datetime, InBgn datetime, InEnd datetime,
        OutBgn datetime, OutEnd datetime, InFact datetime, OutFact datetime)

    /*---------------- 3. 人员范围 ----------------*/
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
   
    IF NOT EXISTS(SELECT 1 FROM #Person) RETURN

    /*---------------- 4. 收集刷卡数据 (fnCollectBrushCard) ----------------*/
    -- 4.1 原始刷卡(仅取有效打卡 Data_Flag<20,取前后一天以处理跨天卡)
    INSERT #BC (Person_ID, bt, fl)
    SELECT Person_ID,
           CASE WHEN CAST(LEFT(Brush_Time,2) AS int) >= 24
                THEN DATEADD(minute, CAST(LEFT(Brush_Time,2) AS int)*60
                      + CAST(SUBSTRING(Brush_Time,4,2) AS int) - 1440,
                      CONVERT(datetime,Brush_Date))
                ELSE DATEADD(minute, CAST(LEFT(Brush_Time,2) AS int)*60
                      + CAST(SUBSTRING(Brush_Time,4,2) AS int),
                      CONVERT(datetime,Brush_Date)) END,
           Data_Flag
    FROM KQ_Download
    WHERE Data_Flag < 20
      AND Brush_Date >= DATEADD(day,-1,@DateBgn) AND Brush_Date <= DATEADD(day,1,@DateEnd)
      AND Person_ID IN (SELECT Person_ID FROM #Person)

    -- 4.2 补刷卡(启用审核时仅取已审核单据)
    INSERT #BC (Person_ID, bt, fl)
    SELECT st.Person_ID,
           CASE WHEN CAST(LEFT(st.Supply_Time,2) AS int) >= 24
                THEN DATEADD(minute, CAST(LEFT(st.Supply_Time,2) AS int)*60
                      + CAST(SUBSTRING(st.Supply_Time,4,2) AS int) - 1440,
                      CONVERT(datetime,st.Supply_Date))
                ELSE DATEADD(minute, CAST(LEFT(st.Supply_Time,2) AS int)*60
                      + CAST(SUBSTRING(st.Supply_Time,4,2) AS int),
                      CONVERT(datetime,st.Supply_Date)) END,
           st.Supply_Class
    FROM KQ_SupplyTime st
    WHERE st.Supply_Date >= DATEADD(day,-1,@DateBgn) AND st.Supply_Date <= DATEADD(day,1,@DateEnd)
      AND st.Person_ID IN (SELECT Person_ID FROM #Person)
      --AND (@bAudit = 0 OR EXISTS(SELECT 1 FROM AD_Auditing a WHERE a.Bill_TableName = 'KQ_SupplyTime' AND a.ADState = '11111111' AND a.BillIDKEY = st.ID_Key))

    /*---------------- 5. 节假日与异常单据 ----------------*/
    INSERT #Holiday SELECT Holiday_Date, Holiday_Name FROM KQ_Holiday

    INSERT #L (Person_ID, Leave_Date, Leave_Hour, Kind_No, Kind_Name, Is_Deduct, IsOutLeave)
    SELECT a.Person_ID, a.Leave_Date, a.Leave_Hour, b.Kind_No, b.Kind_Name, b.Is_Deduct, a.IsOutLeave
    FROM KQ_Leave a
    LEFT JOIN KQ_LeaveKind b ON b.Kind_No = a.Kind_No
    WHERE a.Leave_Date >= @DateBgn AND a.Leave_Date <= @DateEnd
      AND a.Person_ID IN (SELECT Person_ID FROM #Person)
      -- AND (@bAudit = 0 OR EXISTS(SELECT 1 FROM AD_Auditing x  WHERE x.Bill_TableName = 'KQ_Leave' AND x.ADState = '11111111'  AND x.BillIDKEY = a.ID_Key))

    INSERT #AC SELECT Person_ID, Adjust_Date, Class_No FROM KQ_AdjustClass
    WHERE Adjust_Date >= @DateBgn AND Adjust_Date <= @DateEnd
      AND Person_ID IN (SELECT Person_ID FROM #Person)
      --AND (@bAudit = 0 OR EXISTS(SELECT 1 FROM AD_Auditing x  WHERE x.Bill_TableName = 'KQ_AdjustClass' AND x.ADState = '11111111'  AND x.BillIDKEY = KQ_AdjustClass.ID_Key))

    INSERT #AR SELECT Person_ID, Adjust_Date, Adjust_Class, Adjust_Hour FROM KQ_AdjustRest
    WHERE Adjust_Date >= @DateBgn AND Adjust_Date <= @DateEnd
      AND Person_ID IN (SELECT Person_ID FROM #Person)
      --AND (@bAudit = 0 OR EXISTS(SELECT 1 FROM AD_Auditing x WHERE x.Bill_TableName = 'KQ_AdjustRest' AND x.ADState = '11111111' AND x.BillIDKEY = KQ_AdjustRest.ID_Key))

    INSERT #OT SELECT Person_ID, Over_Date, Over_Hour FROM KQ_OverTime
    WHERE Over_Date >= @DateBgn AND Over_Date <= @DateEnd
      AND Person_ID IN (SELECT Person_ID FROM #Person)
      --AND (@bAudit = 0 OR EXISTS(SELECT 1 FROM AD_Auditing x WHERE x.Bill_TableName = 'KQ_OverTime' AND x.ADState = '11111111' AND x.BillIDKEY = KQ_OverTime.ID_Key))

    INSERT #FA SELECT Person_ID, Fill_Date, Fill_Class, Fill_Hour FROM KQ_FillAttend
    WHERE Fill_Date >= @DateBgn AND Fill_Date <= @DateEnd
      AND Person_ID IN (SELECT Person_ID FROM #Person)
      --AND (@bAudit = 0 OR EXISTS(SELECT 1 FROM AD_Auditing x  WHERE x.Bill_TableName = 'KQ_FillAttend' AND x.ADState = '11111111' AND x.BillIDKEY = KQ_FillAttend.ID_Key))

    /*=====================================================================
      6. 逐人逐日统计 (fnCycleDealPerson / fnCalcEveryDateData)
    =====================================================================*/
    DECLARE @pid int, @PClass varchar(20), @PName varchar(50)
    DECLARE @Mode int, @OTMode int, @RestMode int, @RestDays varchar(20)
    DECLARE @OnWork varchar(10), @OffWork varchar(10)
    DECLARE @IsOTAsWork bit, @IsHolidayAsWork bit

    DECLARE cP CURSOR FAST_FORWARD FOR
        SELECT A.Person_ID, B.Person_Name, A.Class_No, A.Worktime_Mode, A.Overtime_Mode,
               A.Rest_Mode, A.Rest_Days,
               CONVERT(char(10),B.OnWork_Date,120), CONVERT(char(10),B.OffWork_Date,120),
               A.IsOverTimeAsWorkTime, A.IsHolidayAsWorkTime
        FROM KQ_Person A
        LEFT JOIN ST_Person B ON B.Person_ID = A.Person_ID
        WHERE A.Person_ID IN (SELECT Person_ID FROM #Person)
        ORDER BY A.Class_No, A.Person_ID
    OPEN cP
    FETCH NEXT FROM cP INTO @pid,@PName,@PClass,@Mode,@OTMode,@RestMode,@RestDays,
                            @OnWork,@OffWork,@IsOTAsWork,@IsHolidayAsWork
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF @PClass IS NOT NULL AND LTRIM(RTRIM(@PClass)) <> ''
        BEGIN
            /* 当前员工刷卡装入 #BCP (跨日延续的刷卡指针 @CardIdx 对应此序号) */
            DELETE #BCP
            INSERT #BCP (seq, bt, fl)
            SELECT ROW_NUMBER() OVER (ORDER BY bt), bt, fl FROM #BC
            WHERE Person_ID = @pid

            DECLARE @d datetime, @DBgn datetime, @DEnd datetime
            DECLARE @ds varchar(10), @Cls varchar(20), @t varchar(1), @DoCalc int, @Matched int
            DECLARE @CardIdx int, @EndSeq int, @BD varchar(255), @Res int
            DECLARE @PriorCls varchar(20)
            SET @DBgn = CONVERT(datetime,@DateBgn) SET @DEnd = CONVERT(datetime,@DateEnd)
            SET @CardIdx = 1 SET @PriorCls = ''
            SET @d = @DBgn
            WHILE @d <= @DEnd
            BEGIN
                SET @ds = CONVERT(char(10),@d,120)
                SET @Cls = NULL SET @DoCalc = 0 SET @Matched = 0

                /* 未上岗或已离职跳过 (fnDateWorking) */
                IF NOT (@ds < ISNULL(@OnWork,'') OR (@OffWork IS NOT NULL AND @ds >= @OffWork))
                BEGIN
                    IF EXISTS(SELECT 1 FROM #AC WHERE Person_ID=@pid AND Adjust_Date=@ds)
                    BEGIN
                        /* 有调班记录,以调班班次为准;调到规律班(B开头)再展开 */
                        SELECT @Cls = Class_No FROM #AC
                        WHERE Person_ID=@pid AND Adjust_Date=@ds
                        IF LEFT(@Cls,1) = 'B'
                            SET @Cls = dbo.udf_KQ_RuleClass(@Cls,@d)
                        IF @Cls IS NOT NULL AND LTRIM(RTRIM(@Cls)) <> '' SET @DoCalc = 1
                    END
                    ELSE
                    BEGIN
                        SET @t = LEFT(@PClass,1)
                        IF @t = 'C'
                        BEGIN
                            /*----- 灵活班: 对每个候选班次匹配刷卡并打分,取最高分 (fnCalcAgileClass) -----*/
                            DECLARE @Agi TABLE (id int IDENTITY(1,1), ClassNo varchar(20))
                            DELETE @Agi
                            INSERT @Agi (ClassNo)
                            SELECT Shift_ClassNo FROM KQ_AgileClassSub
                            WHERE Class_No = @PClass ORDER BY ID_KEY
                            IF EXISTS(SELECT 1 FROM @Agi)
                            BEGIN
                                DECLARE @ai int, @acnt int, @aCls varchar(20)
                                DECLARE @score decimal(18,2), @BestScore decimal(18,2)
                                DECLARE @BestCls varchar(20), @BestEnd int
                                DECLARE @Vaild int, @MustT int, @m int
                                DECLARE @sInDt datetime, @sOutDt datetime, @fIn datetime, @fOut datetime
                                DECLARE @sInCard int, @sOutCard int, @sOT bit, @ClsName varchar(50)
                                SELECT @acnt = COUNT(*) FROM @Agi
                                SET @ai = 1 SET @BestScore = -999999 SET @BestCls = NULL
                                SET @BestEnd = @CardIdx
                                WHILE @ai <= @acnt
                                BEGIN
                                    SELECT @aCls = ClassNo FROM @Agi WHERE id = @ai
                                    EXEC dbo.usp_KQ_MatchBrush @pid,@d,@aCls,@Mode,
                                         @Late3E,@Early3E,@OTBig,@OutAddMinute,
                                         @CardIdx,@EndSeq OUTPUT,@BD OUTPUT,@Res OUTPUT
                                    IF @Res = 1
                                    BEGIN
                                        /* 打分 (fnCalcInScore/fnCalcOutScore/fnCalcClassScore) */
                                        SET @Vaild = 0 SET @MustT = 0 SET @score = 0
                                        DECLARE cS CURSOR FAST_FORWARD FOR
                                            SELECT InDt,OutDt,InCard,OutCard,OT,InFact,OutFact
                                            FROM #Sect ORDER BY SectNo
                                        OPEN cS
                                        FETCH NEXT FROM cS INTO @sInDt,@sOutDt,@sInCard,@sOutCard,@sOT,@fIn,@fOut
                                        WHILE @@FETCH_STATUS = 0
                                        BEGIN
                                            IF @sInDt IS NULL BREAK
                                            SET @MustT = @MustT + 1
                                            IF @fIn IS NOT NULL
                                            BEGIN
                                                SET @score = @score + 10 SET @Vaild = @Vaild + 1
                                                IF @sInCard = 1 SET @score = @score - 2
                                                ELSE IF @sInCard = 2 AND @fIn = @sInDt SET @score = @score - 1
                                                SET @m = DATEDIFF(minute,@sInDt,@fIn)
                                                IF @m > 0
                                                BEGIN
                                                    IF @sOT = 1 AND @m > 90 SET @score = @score - 6
                                                    IF @m > 10 AND @m <= 60
                                                    BEGIN
                                                        SET @Vaild = @Vaild - 1
                                                        IF @m <= 20 SET @m = -ROUND(@m/10.0,4)
                                                        ELSE SET @m = -ROUND(@m/15.0,4)
                                                    END
                                                    ELSE SET @m = -1
                                                    SET @score = @score + @m
                                                END
                                            END
                                            IF @sOutDt IS NULL BREAK
                                            SET @MustT = @MustT + 1
                                            IF @fOut IS NOT NULL
                                            BEGIN
                                                SET @score = @score + 8 SET @Vaild = @Vaild + 1
                                                IF @sOutCard = 1 SET @score = @score - 2
                                                ELSE IF @sOutCard = 2 AND @fOut = @sOutDt SET @score = @score - 1
                                                SET @m = DATEDIFF(minute,@fOut,@sOutDt)
                                                IF @m > 0
                                                BEGIN
                                                    IF @m > 10 SET @Vaild = @Vaild - 1
                                                    IF @m > 5 AND @m <= 30
                                                    BEGIN
                                                        IF @m <= 10 SET @m = -ROUND(@m/5.0,4)
                                                        ELSE SET @m = -ROUND(@m/10.0,4)
                                                    END
                                                    ELSE SET @m = -1
                                                    SET @score = @score + @m
                                                END
                                            END
                                            FETCH NEXT FROM cS INTO @sInDt,@sOutDt,@sInCard,@sOutCard,@sOT,@fIn,@fOut
                                        END
                                        CLOSE cS DEALLOCATE cS
                                        IF @MustT > 0 SET @score = @score + ROUND(@Vaild*5.0/@MustT,4)
                                        IF @aCls = @PriorCls SET @score = @score + 2
                                        SELECT @ClsName = Class_Name FROM KQ_Class WHERE Class_No = @aCls
                                        IF @MustT = 0 OR @ClsName LIKE '%X%' SET @score = 5.8
                                        IF @score > @BestScore
                                        BEGIN
                                            SET @BestScore = @score SET @BestCls = @aCls
                                            SET @BestEnd = @EndSeq
                                            DELETE #SectBest
                                            INSERT #SectBest SELECT * FROM #Sect
                                        END
                                    END
                                    SET @ai = @ai + 1
                                END
                                IF @BestCls IS NOT NULL
                                BEGIN
                                    SET @Cls = @BestCls SET @CardIdx = @BestEnd
                                    SET @PriorCls = @BestCls
                                    DELETE #Sect INSERT #Sect SELECT * FROM #SectBest
                                    SET @DoCalc = 1 SET @Matched = 1
                                END
                            END
                        END
                        ELSE
                        BEGIN
                            /* 正常班/规律班/自定义班 */
                            IF @t = 'A' SET @Cls = @PClass
                            ELSE IF @t = 'B' SET @Cls = dbo.udf_KQ_RuleClass(@PClass,@d)
                            ELSE
                            BEGIN
                                SELECT @Cls = Day_ClassNo FROM KQ_DefineClassSub
                                WHERE Class_No = @PClass AND Worktime_Date = @ds
                                IF LTRIM(RTRIM(ISNULL(@Cls,''))) = '' SET @Cls = NULL
                            END
                            IF @Cls IS NOT NULL SET @DoCalc = 1
                        END
                    END

                    IF @DoCalc = 1
                    BEGIN
                        IF @Matched = 0
                            EXEC dbo.usp_KQ_MatchBrush @pid,@d,@Cls,@Mode,
                                 @Late3E,@Early3E,@OTBig,@OutAddMinute,
                                 @CardIdx,@EndSeq OUTPUT,@BD OUTPUT,@Res OUTPUT
                        IF @Res = 1
                        BEGIN
                            SET @CardIdx = @EndSeq
                            GOTO LBL_CALC
                        END
                    END
                END
                GOTO LBL_NEXT

/*=======================================================================
  计算当日考勤并写入 #Daily
  (fnInitDailyInfo / fnCalcDailyData / fnWorkInClass / fnWorkInOneCard /
   fnWorkInBrushTime / fnDealWithRestMode / fnDealWithLeaveData /
   fnCalcOtherException / fnInsertOneDateData)
=======================================================================*/
LBL_CALC:
                DECLARE @k int, @Must decimal(18,4), @Fact decimal(18,4), @OT decimal(18,4)
                DECLARE @OverAbs decimal(18,4), @Absent decimal(18,4)
                DECLARE @Late int, @Early int, @LateData varchar(20), @EarlyData varchar(20)
                DECLARE @Reason varchar(60), @LvHour decimal(18,2), @LvData varchar(60)
                DECLARE @LvName varchar(40), @OTCls varchar(1)
                DECLARE @S1Must decimal(18,4), @S1Fact decimal(18,4)
                DECLARE @S1Late int, @S1Early int, @S1LateData varchar(20), @S1EarlyData varchar(20)
                SET @Must=0 SET @Fact=0 SET @OT=0 SET @OverAbs=0
                SET @Late=0 SET @Early=0 SET @LateData='' SET @EarlyData=''
                SET @Reason='' SET @LvHour=0 SET @LvData='' SET @LvName='' SET @OTCls=''
                SET @S1Must=0 SET @S1Fact=0 SET @S1Late=0 SET @S1Early=0
                SET @S1LateData='' SET @S1EarlyData=''

                /*---------------- 6.1 按考勤方式计算原始工时 ----------------*/
                IF @Mode IN (2,4)
                BEGIN
                    /* fnWorkInOneCard: 2=一天只刷一次卡 4=无刷卡计算工时 */
                    DECLARE @li int SET @li = 0
                    SET @k = 1
                    WHILE @k <= 5
                    BEGIN
                        SELECT @sInDt=InDt,@sOutDt=OutDt,@sOT=OT,@fIn=InFact,@fOut=OutFact,
                               @sInCard=InCard,@sOutCard=OutCard FROM #Sect WHERE SectNo=@k
                        IF @sInDt IS NULL BREAK
                        IF @sOT = 0 SET @Must = @Must + DATEDIFF(minute,@sInDt,@sOutDt)
                                               - (SELECT Deduct FROM #Sect WHERE SectNo=@k)
											  
                        IF @Mode = 2 AND @li = 0
                           AND ((@sInCard<>2 AND @fIn IS NOT NULL)
                             OR (@sOutCard<>2 AND @fOut IS NOT NULL)) SET @li = 100
                        SET @k = @k + 1
                    END
                    IF @Mode = 2 AND @li = 0 SET @Fact = 0 ELSE SET @Fact = @Must
                    IF @Mode = 4   /* 无刷卡计算工时: 直接格式化,不再处理异常 */
                    BEGIN
                        SET @Must = dbo.udf_KQ_Fmt(@Must,@MinToHour,@OneHour,@HalfHour,@Dec)
                        SET @Fact = dbo.udf_KQ_Fmt(@Fact,@MinToHour,@OneHour,@HalfHour,@Dec)
                        SET @Absent = @Must - @Fact
                        SET @OT = dbo.udf_KQ_Fmt(@OT,@MinToHour,@OneHour,@HalfHour,@Dec)
                        GOTO LBL_INSERT
                    END
                END
                ELSE IF @Mode = 3
                BEGIN
                    /* fnWorkInBrushTime: 只统计刷卡时间 */
                    SET @li = 0
                    SET @k = 1
                    WHILE @k <= 5
                    BEGIN
                        SELECT @fIn=InFact,@fOut=OutFact FROM #Sect WHERE SectNo=@k
                        IF @fIn IS NOT NULL AND @fOut IS NOT NULL
                            SET @li = @li + DATEDIFF(minute,@fIn,@fOut)
                        SET @k = @k + 1
                    END
                    SET @Fact = @li SET @Must = @Fact   /* 应出勤=实出勤 */
                END
                ELSE
                BEGIN
                    /* fnWorkInClass: 两次刷卡(含加班段) */
					DECLARE @sDeduct int, @sMust decimal(18,4), @sFact decimal(18,4), @sOTv decimal(18,4)
					/*需注释*/
					--select * from #Sect
                    SET @k = 1
                    WHILE @k <= 5
                    BEGIN
                        SELECT @sInDt=InDt,@sOutDt=OutDt,@sDeduct=Deduct,@sOT=OT,
                               @fIn=InFact,@fOut=OutFact FROM #Sect WHERE SectNo=@k
                        IF @sInDt IS NULL BREAK
                        SET @sMust = DATEDIFF(minute,@sInDt,@sOutDt) - @sDeduct
                        IF @sOT = 0
                        BEGIN
                            DECLARE @sLate int, @sEarly int
                            DECLARE @sLateData varchar(20), @sEarlyData varchar(20)
                            DECLARE @ldIn datetime, @ldOut datetime
                            SET @sLate=0 SET @sEarly=0 SET @sLateData='' SET @sEarlyData=''
                            IF @fIn IS NOT NULL AND @fOut IS NOT NULL
                            BEGIN
                                /* 迟到 (fnCalcLateData) */
                                SET @ldIn = @fIn
                                IF @fIn > @sInDt
                                BEGIN
                                    SET @m = DATEDIFF(minute,@sInDt,@fIn)
                                    IF @m <= @Late3E
                                    BEGIN
                                        IF @m >= @Late1S
                                        BEGIN
                                            SET @sLate = @m
                                            SET @sLateData = CASE WHEN @m<=@Late1E THEN '1/'
                                                                  WHEN @m<=@Late2E THEN '2/'
                                                                  ELSE '3/' END
                                                + CAST(@m AS varchar) + ','
                                        END
                                        ELSE SET @ldIn = @sInDt
                                    END
                                END
                                ELSE SET @ldIn = @sInDt
                                /* 早退 (fnCalcEarlyData) */
                                SET @ldOut = @fOut
                                IF @fOut < @sOutDt
                                BEGIN
                                    SET @m = DATEDIFF(minute,@fOut,@sOutDt)
                                    IF @m <= @Early3E
                                    BEGIN
                                        IF @m >= @Early1S
                                        BEGIN
                                            SET @sEarly = @m
                                            SET @sEarlyData = CASE WHEN @m<=@Early1E THEN '1/'
                                                                   WHEN @m<=@Early2E THEN '2/'
                                                                   ELSE '3/' END
                                                + CAST(@m AS varchar) + ','
                                        END
                                        ELSE SET @ldOut = @sOutDt
                                    END
                                END
                                ELSE SET @ldOut = @sOutDt
                                SET @sFact = dbo.udf_KQ_FactHour(@sMust,
                                                 DATEDIFF(minute,@ldIn,@ldOut), @sDeduct)
--print 'print @@sFact-0 ' print @ldIn  print @ldOut 
                            END
                            ELSE SET @sFact = 0
                            SET @Must = @Must + isnull(@sMust,0) SET @Fact = @Fact + isnull(@sFact,0)
							
							--print 'print @@Must-0 ' +cast(@Must as varchar)
                            SET @Late = @Late + @sLate SET @LateData = @LateData + @sLateData
                            SET @Early = @Early + @sEarly SET @EarlyData = @EarlyData + @sEarlyData
                            IF @S1Must = 0
                            BEGIN
                                DECLARE @nOutDt datetime, @nOT bit
								SET @nOutDt = NULL
								SET @nOT = NULL
                                SELECT @nOutDt=OutDt,@nOT=OT FROM #Sect WHERE SectNo=@k+1
                                IF @nOutDt IS NULL OR @nOT = 1
                                BEGIN
                                    SET @S1Must = @sMust/2.0 SET @S1Fact = @sFact/2.0
                                    SET @S1Early = 0 SET @S1EarlyData = ''
                                END
                                ELSE
                                BEGIN
                                    SET @S1Must = @sMust SET @S1Fact = @sFact
                                    SET @S1Early = @sEarly SET @S1EarlyData = @sEarlyData
                                END
                                SET @S1Late = @sLate SET @S1LateData = @sLateData
                            END
                        END
                        ELSE
                        BEGIN
                            /* 加班段 (fnCalcOvertimeData) */
                            SET @sOTv = 0
                            IF @OTMode <> 5 AND @fIn IS NOT NULL AND @fOut IS NOT NULL
                            BEGIN
                                SET @ldIn = @fIn SET @ldOut = @fOut
                                IF @OTMode <> 2
                                BEGIN
                                    IF @fIn < @sInDt SET @ldIn = @sInDt
                                    IF @fOut > @sOutDt SET @ldOut = @sOutDt
                                END
                                SET @sOTv = DATEDIFF(minute,@ldIn,@ldOut)
                                IF @OTMode <> 4
                                    SET @sOTv = dbo.udf_KQ_FactHour(@sMust,@sOTv,@sDeduct)
                            END
                            IF @OTMode = 3
                            BEGIN SET @OverAbs = @OverAbs + @sMust - @sOTv SET @sOTv = 0 END
                            SET @OT = @OT + @sOTv
                        END
                        SET @k = @k + 1
						--print 'print @Fact-1 ' +cast(@Fact as varchar)
                    END
					--print 'print @Fact-2 ' + cast(@Fact as varchar)
                END
				
                /*---------------- 6.2 休息/节假日处理 (fnDealWithRestMode) ----------------*/
                IF @Mode <> 4
                BEGIN
                    IF @Mode <> 3
                    BEGIN
                        DECLARE @isHol bit, @HolName varchar(50), @dow int, @dy varchar(2), @i int, @lv decimal(18,4)
                        SET @isHol = 0 SET @HolName = ''
                        SELECT @isHol = 1, @HolName = Holiday_Name FROM #Holiday
                        WHERE Holiday = SUBSTRING(@ds,6,5)
                        /* 节假日且未登记调休补班 -> 全部计为节假日加班 J */
                        IF @isHol = 1 AND NOT EXISTS(SELECT 1 FROM #AR
                               WHERE Person_ID=@pid AND Adjust_Date=@ds AND Adjust_Class=1)
                        BEGIN
                            SET @OT = @OT + @Fact SET @OTCls = 'J' SET @Must = 0 SET @Fact = 0
                            SET @Early = 0 SET @Late = 0 SET @LateData = '' SET @EarlyData = ''
                            SET @Absent = 0
                            SET @Reason = @Reason + ',' + @HolName
                            GOTO LBL_AFTER_REST
                        END

                        SET @dow = (DATEDIFF(day,'20000103',@d) % 7) + 1  -- 周一=1..周日=7
                        SET @dy = RIGHT('0' + CAST(DAY(@d) AS varchar),2)
                        SET @i = CHARINDEX(CAST(@dow AS varchar), @RestDays)
                        IF (@RestMode = 1 AND @i > 0 AND SUBSTRING(@RestDays,@i-1,1) <> '.')
                           OR (@RestMode = 2 AND CHARINDEX(@dy,@RestDays) > 0)
                           OR @isHol = 1
                        BEGIN
                            IF @isHol = 1 SET @OTCls = 'J' ELSE SET @OTCls = 'X'
                            IF EXISTS(SELECT 1 FROM #AR WHERE Person_ID=@pid
                                      AND Adjust_Date=@ds AND Adjust_Class=1)
                            BEGIN
                                SELECT @lv = Adjust_Hour FROM #AR WHERE Person_ID=@pid
                                AND Adjust_Date=@ds AND Adjust_Class=1
                                IF @RestMode = 1 AND @isHol = 0
                                   AND SUBSTRING(@RestDays,@i+1,1) = '.'
                                BEGIN
                                    /* 休息半天的调休补班 (fnRestHalfDay) */
                                    SET @Must = @S1Must + @lv*60
                                    IF @lv <> 0
                                    BEGIN
                                        IF @Fact > @Must
                                        BEGIN SET @OT = @OT + (@Fact - @Must) SET @Fact = @Must END
                                        SET @OTCls = 'X'
                                    END
                                    ELSE SET @OTCls = 'P'
                                    SET @Reason = @Reason + ',' + @S_FillClass
                                    SET @Late = @S1Late SET @Early = @S1Early
                                    SET @LateData = @S1LateData SET @EarlyData = @S1EarlyData
                                END
                                ELSE
                                BEGIN
                                    /* 休息全天来上班的调休补班 (fnRestOneDay) */
                                    IF @lv <> 0
                                    BEGIN
                                        SET @Must = @lv * 60
                                        IF @Fact > @Must
                                        BEGIN SET @OT = @OT + (@Fact - @Must) SET @Fact = @Must END
                                        SET @OTCls = 'X'
                                    END
                                    ELSE SET @OTCls = 'P'
                                    SET @Reason = @Reason + ',' + @S_FillClass
                                END
                                IF @isHol = 1 SET @OTCls = 'J'
                            END
                            ELSE IF @RestMode = 1 AND @isHol = 0 AND @i > 0
                                    AND SUBSTRING(@RestDays,@i+1,1) = '.'
                            BEGIN
                                /* 休息半天 (fnRestHalfDay 无补班): 只计第1段 */
                                SET @Must = @S1Must
                                IF @Fact > @Must
                                BEGIN SET @OT = @OT + (@Fact - @S1Fact) SET @Fact = @S1Fact END
                                SET @Late = @S1Late SET @Early = @S1Early
                                SET @LateData = @S1LateData SET @EarlyData = @S1EarlyData
                            END
                            ELSE
                            BEGIN
                                /* 休息全天 (fnRestOneDay 无补班): 全部计加班 */
                                SET @OT = @OT + @Fact SET @Must = 0 SET @Fact = 0
                                SET @Early = 0 SET @Late = 0
                                SET @LateData = '' SET @EarlyData = ''
                                SET @Absent = 0
                            END
                            IF @OTMode = 4 SET @OT = 0
                        END
                        ELSE SET @OTCls = 'P'
LBL_AFTER_REST:
                        IF @OTMode = 5 SET @OT = 0   /* 不计加班 */
                    END

                    /* 格式化(分钟->小时)并计算缺勤 */
                    SET @Must = dbo.udf_KQ_Fmt(@Must,@MinToHour,@OneHour,@HalfHour,@Dec)
                    SET @Fact = dbo.udf_KQ_Fmt(@Fact,@MinToHour,@OneHour,@HalfHour,@Dec)
                    SET @OT = dbo.udf_KQ_Fmt(@OT,@MinToHour,@OneHour,@HalfHour,@Dec)
                    SET @OverAbs = dbo.udf_KQ_Fmt(@OverAbs,@MinToHour,@OneHour,@HalfHour,@Dec)
                    SET @Absent = @Must - @Fact
                        - dbo.udf_KQ_Fmt(@Late + @Early,@MinToHour,@OneHour,@HalfHour,@Dec)
                        + @OverAbs
                    IF @Absent < 0 SET @Absent = 0
                    IF (@Late + @Early > 0) AND @IsDeducte = 0
                        SET @Fact = @Fact
                          + dbo.udf_KQ_Fmt(@Late + @Early,@MinToHour,@OneHour,@HalfHour,@Dec)

                    /*---------------- 6.3 请假处理 (fnDealWithLeaveData) ----------------*/
                    IF EXISTS(SELECT 1 FROM #L WHERE Person_ID=@pid AND Leave_Date=@ds)
                    BEGIN
                        DECLARE @isDed bit, @isOut bit, @kNo varchar(10), @kName varchar(50)
                        DECLARE @lv0 decimal(18,4), @valx int
                        DECLARE cL CURSOR FAST_FORWARD FOR
                            SELECT Leave_Hour, Is_Deduct, IsOutLeave, Kind_No, Kind_Name
                            FROM #L WHERE Person_ID=@pid AND Leave_Date=@ds ORDER BY rid
                        OPEN cL
                        FETCH NEXT FROM cL INTO @lv,@isDed,@isOut,@kNo,@kName
                        WHILE @@FETCH_STATUS = 0
                        BEGIN
                            IF @lv = 0 SET @lv = @Must   /* 请假小时=0 视为全天 */
                            SET @lv0 = @lv
                            IF @Absent > 0
                            BEGIN
                                /* 先抵消加班造成的缺勤 */
                                IF @OverAbs > 0
                                BEGIN
                                    IF @lv >= @OverAbs
                                    BEGIN SET @lv=@lv-@OverAbs SET @Absent=@Absent-@OverAbs SET @OverAbs=0 END
                                    ELSE BEGIN SET @Absent=@Absent-@lv SET @OverAbs=@OverAbs-@lv SET @lv=0 END
                                END
                                IF @lv > 0
                                BEGIN
                                    IF @lv > @Absent AND @LeaveDatum = 0
                                    BEGIN SET @lv = @Absent SET @lv0 = @lv END
                                    IF @isDed = 1   /* 扣全勤 */
                                    BEGIN IF @isOut = 1 SET @Fact=@Fact-isnull(@lv,0) ELSE SET @Absent=@Absent-@lv END
                                    ELSE IF @isOut = 0  /* 不扣全勤且非外出请假 */
                                    BEGIN SET @Absent=@Absent-@lv SET @Fact=@Fact+isnull(@lv,0) END
                                    /* 请假抵扣早退迟到 (fnLeaveOutLateEarly) */
                                    IF @Absent <= 0 AND @IsDeductLE = 1
                                       AND ((@Early + @Absent*60 >= 0)
                                            OR ABS(@Late + @Early + @Absent*60) <= 60)
                                    BEGIN
                                        SET @valx = FLOOR(-@Absent*60)
                                        IF @valx>0 AND (@Late+@Early>0) AND @IsDeducte=0
                                            SET @Fact = @Fact - isnull(ROUND((@Late+@Early)/60.0,1),0)
                                        IF @Late>0 AND @valx>0
                                        BEGIN SET @Late=@Late-@valx IF @Late<0 BEGIN SET @valx=-@Late SET @Late=0 END ELSE SET @valx=0 END
                                        IF @Early>0 AND @valx>0
                                        BEGIN SET @Early=@Early-@valx IF @Early<0 BEGIN SET @valx=-@Early SET @Early=0 END ELSE SET @valx=0 END
                                        IF @valx>0 SET @Absent = -ROUND(@valx/60.0,1) ELSE SET @Absent = 0
                                    END
                                    IF @Absent < 0 AND @LeaveDatum = 1 SET @Fact = @Fact + isnull(@Absent,0)
                                END
                            END
                            ELSE
                            BEGIN
                                IF @IsDeductLE = 1
                                BEGIN
                                    SET @valx = FLOOR(@lv*60)
                                    IF @valx>0 AND (@Late+@Early>0) AND @IsDeducte=0
                                        SET @Fact = @Fact - isnull(ROUND((@Late+@Early)/60.0,1),0)
                                    IF @Late>0 AND @valx>0
                                    BEGIN SET @Late=@Late-@valx IF @Late<0 BEGIN SET @valx=-@Late SET @Late=0 END ELSE SET @valx=0 END
                                    IF @Early>0 AND @valx>0
                                    BEGIN SET @Early=@Early-@valx IF @Early<0 BEGIN SET @valx=-@Early SET @Early=0 END ELSE SET @valx=0 END
                                    IF @valx>0 SET @Absent = -ROUND(@valx/60.0,1) ELSE SET @Absent = 0
                                END
                                IF @isDed = 1 SET @Fact = @Fact - isnull(@lv,0)
                            END
                            IF @Fact < 0 SET @Fact = 0
                            IF @Absent < 0 SET @Absent = 0
                            IF @OverAbs < 0 SET @OverAbs = 0
                            IF @Fact > @Must SET @Fact = @Must
                            SET @LvHour = @LvHour + @lv0
                            SET @LvData = @LvData + @kNo + '/' + CAST(@lv0 AS varchar) + ','
                            SET @LvName = @LvName + @kName + ','
                            FETCH NEXT FROM cL INTO @lv,@isDed,@isOut,@kNo,@kName
                        END
                        CLOSE cL DEALLOCATE cL
                    END

                    /*---------------- 6.4 其他异常 (fnCalcOtherException) ----------------*/
                    /* 补考勤(填旷工) */
                    IF @Absent > 0 AND EXISTS(SELECT 1 FROM #FA
                           WHERE Person_ID=@pid AND Fill_Date=@ds AND Fill_Class=0)
                    BEGIN
                        SELECT @lv = Fill_Hour FROM #FA
                        WHERE Person_ID=@pid AND Fill_Date=@ds AND Fill_Class=0
                        SET @Fact = @Fact + isnull(@lv,0)
                        IF @Fact > @Must SET @Fact = @Must
                        SET @Absent = @Absent - @lv
                        SET @Reason = @Reason + ',' + @S_FillAttend
                    END
                    /* 调休 */
                    IF EXISTS(SELECT 1 FROM #AR WHERE Person_ID=@pid
                              AND Adjust_Date=@ds AND Adjust_Class=0)
                    BEGIN
                        SELECT @lv = Adjust_Hour FROM #AR
                        WHERE Person_ID=@pid AND Adjust_Date=@ds AND Adjust_Class=0
                        IF @lv = 0 OR @lv >= @Must
                        BEGIN
                            SET @OT = @OT + @Fact SET @OTCls = 'X' SET @Must = 0 SET @Fact = 0
                            SET @Early = 0 SET @Late = 0
                            SET @LateData = '' SET @EarlyData = ''
                            SET @Absent = 0
                            IF @OTMode = 4 SET @OT = 0
                        END
                        ELSE
                        BEGIN
                            SET @Must = @Must - @lv
                            IF @Must <= 0 BEGIN SET @Must = 0 SET @OTCls = 'X' END
                            IF @Fact > @Must
                            BEGIN
                                IF @OTMode < 4 SET @OT = @OT + (@Fact - @Must) ELSE SET @OT = 0
                                SET @Fact = @Must
                            END
                            SET @Absent = @Absent - @lv
                        END
                        SET @Reason = @Reason + ',' + @S_AdjustRest
                    END
                    /* 加班登记审核: 以登记时长为准取小 */
                    IF @OTDatum = 1 OR @OTMode = 6
                    BEGIN
                        IF EXISTS(SELECT 1 FROM #OT WHERE Person_ID=@pid AND Over_Date=@ds)
                        BEGIN
                            DECLARE @ov decimal(18,4)
                            SELECT @ov = Over_Hour FROM #OT WHERE Person_ID=@pid AND Over_Date=@ds
                            SET @OT = CASE WHEN @OT < @ov THEN @OT ELSE @ov END
                        END
                        ELSE SET @OT = 0
                    END
                    /* 补加班 */
                    IF EXISTS(SELECT 1 FROM #FA WHERE Person_ID=@pid
                              AND Fill_Date=@ds AND Fill_Class=1)
                    BEGIN
                        SELECT @lv = Fill_Hour FROM #FA
                        WHERE Person_ID=@pid AND Fill_Date=@ds AND Fill_Class=1
                        SET @OT = @OT + @lv
                    END
                    IF @Must = 0 AND @OT > 0 AND @OTCls = 'P' SET @OTCls = 'X'

                    /* 转正数 */
                    IF @Fact < 0 SET @Fact = 0
                    IF @Must < 0 SET @Must = 0
                    IF @OT < 0 SET @OT = 0
                    IF @OverAbs < 0 SET @OverAbs = 0
                    IF @Absent < 0 SET @Absent = 0

                    /* 平时加班转出勤 / 节假日加班转出勤 */
                    IF @IsOTAsWork = 1 AND @OTCls = 'P'
                    BEGIN SET @Must=@Must+@OT SET @Fact=@Fact+isnull(@OT,0) SET @OT=0 SET @OTCls='' END
                    IF @IsHolidayAsWork = 1 AND @OTCls = 'J'
                    BEGIN SET @Must=@OT SET @Fact=@OT SET @OT=0 SET @OTCls='' SET @Reason='' END
                END

/*=======================================================================
  写入 #Daily (fnInsertOneDateData)
=======================================================================*/
LBL_INSERT:
                /* 段实际打卡时间字符串 + 漏刷卡标志 */
                DECLARE @g1i varchar(16),@g1o varchar(16),@g2i varchar(16),@g2o varchar(16)
                DECLARE @g3i varchar(16),@g3o varchar(16),@g4i varchar(16),@g4o varchar(16)
                DECLARE @g5i varchar(16),@g5o varchar(16), @BFF bit
                SET @g1i='' SET @g1o='' SET @g2i='' SET @g2o='' SET @g3i='' SET @g3o=''
                SET @g4i='' SET @g4o='' SET @g5i='' SET @g5o='' SET @BFF = 0
                SET @k = 1
                WHILE @k <= 5
                BEGIN
                    SELECT @sInDt=InDt,@sOutDt=OutDt,@fIn=InFact,@fOut=OutFact
                    FROM #Sect WHERE SectNo=@k
                    IF @Mode <> 3 AND (@sInDt IS NULL OR @sOutDt IS NULL) BREAK
                    IF @fIn IS NOT NULL
                    BEGIN
                        IF @k=1 SET @g1i = CONVERT(char(16),@fIn,120)
                        IF @k=2 SET @g2i = CONVERT(char(16),@fIn,120)
                        IF @k=3 SET @g3i = CONVERT(char(16),@fIn,120)
                        IF @k=4 SET @g4i = CONVERT(char(16),@fIn,120)
                        IF @k=5 SET @g5i = CONVERT(char(16),@fIn,120)
                        IF @fOut IS NULL SET @BFF = 1
                    END
                    IF @fOut IS NOT NULL
                    BEGIN
                        IF @k=1 SET @g1o = CONVERT(char(16),@fOut,120)
                        IF @k=2 SET @g2o = CONVERT(char(16),@fOut,120)
                        IF @k=3 SET @g3o = CONVERT(char(16),@fOut,120)
                        IF @k=4 SET @g4o = CONVERT(char(16),@fOut,120)
                        IF @k=5 SET @g5o = CONVERT(char(16),@fOut,120)
                        IF @fIn IS NULL SET @BFF = 1
                    END
                    SET @k = @k + 1
                END

                /* 异常原因串 */
                IF @OT <= 0 SET @OTCls = ''
                IF @Absent > 0 SET @Reason = @Reason + ',' + @S_Absent
                IF @LvHour > 0 SET @Reason = @Reason + ',' + @S_Leave
                IF @Late > 0 SET @Reason = @Reason + ',' + @S_Late
                IF @Early > 0 SET @Reason = @Reason + ',' + @S_Early
                IF LEN(@Reason) > 0 SET @Reason = SUBSTRING(@Reason,2,60)
                /* 缺勤>1小时且有漏刷卡 -> 记忘刷卡 */
                IF @Absent > 1 AND @BFF = 1 SET @Reason = @S_Forget
                IF @OT*60 <= @OTBig BEGIN SET @OT = 0 SET @OTCls = '' END

                /* 班次时间串(如 08:00-17:00,18:00-21:00) 及星期 */
                DECLARE @ClassData varchar(60), @OfWeek varchar(10)
                SET @ClassData = null/*''*/
                SELECT @ClassData = COALESCE(@ClassData+',','') + TimeIn + '-' + TimeOut
                FROM #Sect WHERE TimeIn IS NOT NULL AND TimeOut IS NOT NULL and len(isnull(TimeIn,''))>3
                ORDER BY SectNo
                SET @OfWeek = CASE (DATEDIFF(day,'20000103',@d)%7)+1
                    WHEN 1 THEN '一' WHEN 2 THEN '二' WHEN 3 THEN '三'
                    WHEN 4 THEN '四' WHEN 5 THEN '五' WHEN 6 THEN '六'
                    ELSE '日' END
                IF @Mode = 4 SET @BD = ''

                INSERT #Daily (Person_ID,Worktime_Date,OfWeek,Class_No,Class_Data,
                    Sect1_InFact,Sect1_OutFact,Sect2_InFact,Sect2_OutFact,
                    Sect3_InFact,Sect3_OutFact,Sect4_InFact,Sect4_OutFact,
                    Sect5_InFact,Sect5_OutFact,
                    Must_Work,Fact_Work,Absent_Work,Reason,Leave_Hour,Leave_Data,
                    Leave_Name,Overtime_Hour,Overtime_Class,Late_Minute,Late_Data,
                    Early_Minute,Early_Data,Brush_Data)
                VALUES (@pid,@ds,@OfWeek,isnull(@Cls,''),@ClassData,
                    NULLIF(@g1i,''),NULLIF(@g1o,''),NULLIF(@g2i,''),NULLIF(@g2o,''),
                    NULLIF(@g3i,''),NULLIF(@g3o,''),NULLIF(@g4i,''),NULLIF(@g4o,''),
                    NULLIF(@g5i,''),NULLIF(@g5o,''),
                    @Must,@Fact,@Absent,NULLIF(@Reason,''),@LvHour,NULLIF(@LvData,''),
                    NULLIF(@LvName,''),@OT,NULLIF(@OTCls,''),@Late,NULLIF(@LateData,''),
                    @Early,NULLIF(@EarlyData,''),isnull(@BD,''))


LBL_NEXT:
                SET @d = DATEADD(day,1,@d)
            END   -- while @d <= @DEnd
        END       -- if 人员有班次
        FETCH NEXT FROM cP INTO @pid,@PName,@PClass,@Mode,@OTMode,@RestMode,@RestDays,
                                @OnWork,@OffWork,@IsOTAsWork,@IsHolidayAsWork
    END             -- while person
    CLOSE cP
    DEALLOCATE cP

    /*=====================================================================
      7. 事务内覆盖写回 KQ_Daily (fnSaveCalcResult)
    =====================================================================*/
    BEGIN TRY
        BEGIN TRAN
        DELETE KQ_Daily
        WHERE Worktime_Date >= @DateBgn AND Worktime_Date <= @DateEnd
          AND Person_ID IN (SELECT Person_ID FROM #Person)

        INSERT KQ_Daily (Person_ID,Worktime_Date,OfWeek,Class_No,Class_Data,
            Sect1_InFact,Sect1_OutFact,Sect2_InFact,Sect2_OutFact,
            Sect3_InFact,Sect3_OutFact,Sect4_InFact,Sect4_OutFact,
            Sect5_InFact,Sect5_OutFact,
            Must_Work,Fact_Work,Absent_Work,Reason,Leave_Hour,Leave_Data,
            Leave_Name,Overtime_Hour,Overtime_Class,Late_Minute,Late_Data,
            Early_Minute,Early_Data,Brush_Data)
        SELECT Person_ID,Worktime_Date,OfWeek,Class_No,Class_Data,
            Sect1_InFact,Sect1_OutFact,Sect2_InFact,Sect2_OutFact,
            Sect3_InFact,Sect3_OutFact,Sect4_InFact,Sect4_OutFact,
            Sect5_InFact,Sect5_OutFact,
            Must_Work,Fact_Work,Absent_Work,Reason,Leave_Hour,Leave_Data,
            Leave_Name,Overtime_Hour,Overtime_Class,Late_Minute,Late_Data,
            Early_Minute,Early_Data,Brush_Data
        FROM #Daily
        COMMIT TRAN
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRAN
        DECLARE @em varchar(4000)
        SET @em = '日考勤统计失败(人员:' + CAST(@pid AS varchar) + ' 日期:' + @ds
                + '): ' + ERROR_MESSAGE()
        RAISERROR(@em,16,1)
        RETURN
    END CATCH

    PRINT '日考勤统计完成: ' + @DateBgn + ' ~ ' + @DateEnd
END
GO

/*=============================================================================
  调用示例:
  EXEC dbo.usp_KQ_CalcDaily '2024-11-02', '2024-11-18', '130'
    EXEC dbo.usp_KQ_CalcDaily '2026-10-01', '2026-10-31', ''              -- 全部人员
    EXEC dbo.usp_KQ_CalcDaily '2026-10-01', '2026-10-31', '101,102,205'   -- 指定人员
=============================================================================*/


