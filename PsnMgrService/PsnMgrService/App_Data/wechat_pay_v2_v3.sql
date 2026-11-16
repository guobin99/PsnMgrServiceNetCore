/*===========================================================
  微信充值 V2/V3 升级脚本（在现有一脸通数据库上执行一次）
  作用：
    1. 确认 ST_SupplyFund.Out_Trade_No 列存在（商户单号，用于幂等入账）
    2. 为 Out_Trade_No 建唯一索引，彻底避免「异步通知 + 前端查询」并发时重复加钱
    3. 为 WX_RechargeLog.Out_Trade_No 建查询索引
  注意：先备份数据库；若历史上已存在重复的非空 Out_Trade_No，
        唯一索引会自动跳过（不会删除任何财务数据），可人工核对后再建。
===========================================================*/

IF NOT EXISTS (SELECT 1 FROM syscolumns WHERE id = OBJECT_ID('ST_SupplyFund') AND name = 'Out_Trade_No')
    ALTER TABLE ST_SupplyFund ADD Out_Trade_No varchar(64) NULL
GO

IF NOT EXISTS (SELECT 1 FROM syscolumns WHERE id = OBJECT_ID('ST_SupplyFundHistory') AND name = 'Out_Trade_No')
    ALTER TABLE ST_SupplyFundHistory ADD Out_Trade_No varchar(64) NULL
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ST_SupplyFund_Out_Trade_No' AND object_id = OBJECT_ID('ST_SupplyFund'))
   AND NOT EXISTS (SELECT Out_Trade_No FROM ST_SupplyFund WHERE Out_Trade_No IS NOT NULL GROUP BY Out_Trade_No HAVING COUNT(1) > 1)
BEGIN
    CREATE UNIQUE INDEX UX_ST_SupplyFund_Out_Trade_No
        ON ST_SupplyFund(Out_Trade_No)
        WHERE Out_Trade_No IS NOT NULL
END
GO

IF OBJECT_ID('WX_RechargeLog') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WX_RechargeLog_Out_Trade_No' AND object_id = OBJECT_ID('WX_RechargeLog'))
BEGIN
    CREATE INDEX IX_WX_RechargeLog_Out_Trade_No ON WX_RechargeLog(Out_Trade_No)
END
GO
