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


