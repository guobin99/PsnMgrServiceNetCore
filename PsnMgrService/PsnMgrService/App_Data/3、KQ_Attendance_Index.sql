/* ============================================================
   考勤接口（/api/m/attendance/daily）用到的索引
   背景：H5「我的考勤」按 人员 + 日期区间 取 KQ_Daily / KQ_DailyHistory，
         CS 建库脚本里只有 Worktime_Date 单列索引（KQ_Daily_DateIndex），
         按 Person_ID 过滤时仍要扫描整个日期段。
   说明：仅新增索引，不修改/删除任何表结构，可安全重复执行。
   ============================================================ */

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_KQ_Daily_Person_Date' AND object_id = OBJECT_ID('dbo.KQ_Daily'))
    CREATE NONCLUSTERED INDEX IX_KQ_Daily_Person_Date
        ON dbo.KQ_Daily (Person_ID, Worktime_Date);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_KQ_DailyHistory_Person_Date' AND object_id = OBJECT_ID('dbo.KQ_DailyHistory'))
    CREATE NONCLUSTERED INDEX IX_KQ_DailyHistory_Person_Date
        ON dbo.KQ_DailyHistory (Person_ID, Worktime_Date);
GO

/* 账套表只取 Account_Flag=0 一条，数据量很小，仅在缺失时补一个过滤索引 */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_KQ_Account_Flag' AND object_id = OBJECT_ID('dbo.KQ_Account'))
    CREATE NONCLUSTERED INDEX IX_KQ_Account_Flag
        ON dbo.KQ_Account (Account_Flag) INCLUDE (Date_Bgn);
GO
