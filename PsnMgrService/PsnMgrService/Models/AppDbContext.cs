using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace PsnMgrService.Models;

public partial class AppDbContext : DbContext
{
    //dotnet ef dbcontext scaffold "Server=127.0.0.1;Database=STCard42;User Id=sa;Password=sa123456;TrustServerCertificate=True;" Microsoft.EntityFrameworkCore.SqlServer -o Models --use-database-names --project="E:\zhimen\2026Ant\PsnMgrService(NetCore)\PsnMgrService\PsnMgrService\PsnMgrService.csproj"
    //dotnet ef dbcontext scaffold "Server=182.92.177.60;Database=STCard42Sample;User Id=sa;Password=sa123456_;TrustServerCertificate=True;" Microsoft.EntityFrameworkCore.SqlServer -o Models --use-database-names --project="E:\zhimen\2026Ant\PsnMgrService(NetCore)\PsnMgrService\PsnMgrService\PsnMgrService.csproj"
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<DC_OrderDataLog> DC_OrderDataLogs { get; set; }

    public virtual DbSet<DC_OrderDataLogSub> DC_OrderDataLogSubs { get; set; }

    public virtual DbSet<DC_OrderDataSub> DC_OrderDataSubs { get; set; }

    public virtual DbSet<DC_OrderDatum> DC_OrderData { get; set; }

    public virtual DbSet<DC_Repast> DC_Repasts { get; set; }

    public virtual DbSet<FK_SysParam> FK_SysParams { get; set; }

    public virtual DbSet<KQ_Account> KQ_Accounts { get; set; }

    public virtual DbSet<KQ_AdjustClass> KQ_AdjustClasses { get; set; }

    public virtual DbSet<KQ_AdjustClassHistory> KQ_AdjustClassHistories { get; set; }

    public virtual DbSet<KQ_AdjustRest> KQ_AdjustRests { get; set; }

    public virtual DbSet<KQ_AdjustRestHistory> KQ_AdjustRestHistories { get; set; }

    public virtual DbSet<KQ_AgileClass> KQ_AgileClasses { get; set; }

    public virtual DbSet<KQ_AgileClassSub> KQ_AgileClassSubs { get; set; }

    public virtual DbSet<KQ_BrushCard> KQ_BrushCards { get; set; }

    public virtual DbSet<KQ_BrushCardHistory> KQ_BrushCardHistories { get; set; }

    public virtual DbSet<KQ_Class> KQ_Classes { get; set; }

    public virtual DbSet<KQ_Daily> KQ_Dailies { get; set; }

    public virtual DbSet<KQ_DailyHistory> KQ_DailyHistories { get; set; }

    public virtual DbSet<KQ_DefineClass> KQ_DefineClasses { get; set; }

    public virtual DbSet<KQ_DefineClassSub> KQ_DefineClassSubs { get; set; }

    public virtual DbSet<KQ_DefineClassSubHistory> KQ_DefineClassSubHistories { get; set; }

    public virtual DbSet<KQ_Download> KQ_Downloads { get; set; }

    public virtual DbSet<KQ_DownloadHistory> KQ_DownloadHistories { get; set; }

    public virtual DbSet<KQ_FillAttend> KQ_FillAttends { get; set; }

    public virtual DbSet<KQ_FillAttendHistory> KQ_FillAttendHistories { get; set; }

    public virtual DbSet<KQ_Holiday> KQ_Holidays { get; set; }

    public virtual DbSet<KQ_InOutDataHistory> KQ_InOutDataHistories { get; set; }

    public virtual DbSet<KQ_InOutDatum> KQ_InOutData { get; set; }

    public virtual DbSet<KQ_Leave> KQ_Leaves { get; set; }

    public virtual DbSet<KQ_LeaveHistory> KQ_LeaveHistories { get; set; }

    public virtual DbSet<KQ_LeaveKind> KQ_LeaveKinds { get; set; }

    public virtual DbSet<KQ_ModifyMonthly> KQ_ModifyMonthlies { get; set; }

    public virtual DbSet<KQ_ModifyMonthlyHistory> KQ_ModifyMonthlyHistories { get; set; }

    public virtual DbSet<KQ_Monthly> KQ_Monthlies { get; set; }

    public virtual DbSet<KQ_MonthlyHistory> KQ_MonthlyHistories { get; set; }

    public virtual DbSet<KQ_Overtime> KQ_Overtimes { get; set; }

    public virtual DbSet<KQ_OvertimeHistory> KQ_OvertimeHistories { get; set; }

    public virtual DbSet<KQ_Person> KQ_People { get; set; }

    public virtual DbSet<KQ_RealInfo> KQ_RealInfos { get; set; }

    public virtual DbSet<KQ_RuleClass> KQ_RuleClasses { get; set; }

    public virtual DbSet<KQ_RuleClassSub> KQ_RuleClassSubs { get; set; }

    public virtual DbSet<KQ_SupplyTime> KQ_SupplyTimes { get; set; }

    public virtual DbSet<KQ_SupplyTimeHistory> KQ_SupplyTimeHistories { get; set; }

    public virtual DbSet<KQ_SysParam> KQ_SysParams { get; set; }

    public virtual DbSet<KQ_TransferTable> KQ_TransferTables { get; set; }

    public virtual DbSet<KQ_Yearly> KQ_Yearlies { get; set; }

    public virtual DbSet<LH_DiscountKind> LH_DiscountKinds { get; set; }

    public virtual DbSet<LH_FeastDiscount> LH_FeastDiscounts { get; set; }

    public virtual DbSet<LH_GetConsume> LH_GetConsumes { get; set; }

    public virtual DbSet<LH_MinuteDiscount> LH_MinuteDiscounts { get; set; }

    public virtual DbSet<LH_SetParamCard> LH_SetParamCards { get; set; }

    public virtual DbSet<LH_TimeBrushCard> LH_TimeBrushCards { get; set; }

    public virtual DbSet<LH_TimeLenDiscount> LH_TimeLenDiscounts { get; set; }

    public virtual DbSet<LH_TimePartDiscount> LH_TimePartDiscounts { get; set; }

    public virtual DbSet<LH_WeekDiscount> LH_WeekDiscounts { get; set; }

    public virtual DbSet<MJ_AbnormRecord> MJ_AbnormRecords { get; set; }

    public virtual DbSet<MJ_AlarmOut> MJ_AlarmOuts { get; set; }

    public virtual DbSet<MJ_AlarmRecord> MJ_AlarmRecords { get; set; }

    public virtual DbSet<MJ_CardManager> MJ_CardManagers { get; set; }

    public virtual DbSet<MJ_ChangeHistory> MJ_ChangeHistories { get; set; }

    public virtual DbSet<MJ_ControlInfo> MJ_ControlInfos { get; set; }

    public virtual DbSet<MJ_ControlInfoTemp> MJ_ControlInfoTemps { get; set; }

    public virtual DbSet<MJ_Controller> MJ_Controllers { get; set; }

    public virtual DbSet<MJ_CtrlDot> MJ_CtrlDots { get; set; }

    public virtual DbSet<MJ_DVSVideoInfo> MJ_DVSVideoInfos { get; set; }

    public virtual DbSet<MJ_Door> MJ_Doors { get; set; }

    public virtual DbSet<MJ_DoorMultiCard> MJ_DoorMultiCards { get; set; }

    public virtual DbSet<MJ_DoorSpecDate> MJ_DoorSpecDates { get; set; }

    public virtual DbSet<MJ_DoorSpecTime> MJ_DoorSpecTimes { get; set; }

    public virtual DbSet<MJ_DoorSpecWeek> MJ_DoorSpecWeeks { get; set; }

    public virtual DbSet<MJ_DoorSwitchTime> MJ_DoorSwitchTimes { get; set; }

    public virtual DbSet<MJ_DoorSwitchTimeSub> MJ_DoorSwitchTimeSubs { get; set; }

    public virtual DbSet<MJ_DotLinkage> MJ_DotLinkages { get; set; }

    public virtual DbSet<MJ_EventInfo> MJ_EventInfos { get; set; }

    public virtual DbSet<MJ_ExtendFunc> MJ_ExtendFuncs { get; set; }

    public virtual DbSet<MJ_FPDoorTimeMerger> MJ_FPDoorTimeMergers { get; set; }

    public virtual DbSet<MJ_FPPsnDoorRight> MJ_FPPsnDoorRights { get; set; }

    public virtual DbSet<MJ_FPTime> MJ_FPTimes { get; set; }

    public virtual DbSet<MJ_FPTimeDetail> MJ_FPTimeDetails { get; set; }

    public virtual DbSet<MJ_FPTimeGroup> MJ_FPTimeGroups { get; set; }

    public virtual DbSet<MJ_FPTimeMerger> MJ_FPTimeMergers { get; set; }

    public virtual DbSet<MJ_Invitation> MJ_Invitations { get; set; }

    public virtual DbSet<MJ_MapDot> MJ_MapDots { get; set; }

    public virtual DbSet<MJ_MapInfo> MJ_MapInfos { get; set; }

    public virtual DbSet<MJ_MeetingInfo> MJ_MeetingInfos { get; set; }

    public virtual DbSet<MJ_MeetingPerson> MJ_MeetingPeople { get; set; }

    public virtual DbSet<MJ_NormalRecord> MJ_NormalRecords { get; set; }

    public virtual DbSet<MJ_Person> MJ_People { get; set; }

    public virtual DbSet<MJ_PersonLastInfo> MJ_PersonLastInfos { get; set; }

    public virtual DbSet<MJ_PsnDoorRight> MJ_PsnDoorRights { get; set; }

    public virtual DbSet<MJ_PsnInOutTime> MJ_PsnInOutTimes { get; set; }

    public virtual DbSet<MJ_PsnInOutTimeSub> MJ_PsnInOutTimeSubs { get; set; }

    public virtual DbSet<MJ_PsnSpecDate> MJ_PsnSpecDates { get; set; }

    public virtual DbSet<MJ_PsnSpecTime> MJ_PsnSpecTimes { get; set; }

    public virtual DbSet<MJ_ResParam> MJ_ResParams { get; set; }

    public virtual DbSet<MJ_Solution> MJ_Solutions { get; set; }

    public virtual DbSet<MJ_SuperRight> MJ_SuperRights { get; set; }

    public virtual DbSet<MJ_SysParam> MJ_SysParams { get; set; }

    public virtual DbSet<MJ_UrgencyGroup> MJ_UrgencyGroups { get; set; }

    public virtual DbSet<MJ_UrgencyGroupSub> MJ_UrgencyGroupSubs { get; set; }

    public virtual DbSet<MJ_vi_ChangeHistory> MJ_vi_ChangeHistories { get; set; }

    public virtual DbSet<SF_MocConsumeMode> SF_MocConsumeModes { get; set; }

    public virtual DbSet<SF_OrderArea> SF_OrderAreas { get; set; }

    public virtual DbSet<SF_OrderDataSub> SF_OrderDataSubs { get; set; }

    public virtual DbSet<SF_OrderDatum> SF_OrderData { get; set; }

    public virtual DbSet<SF_OrderMoc> SF_OrderMocs { get; set; }

    public virtual DbSet<SF_OrderRule> SF_OrderRules { get; set; }

    public virtual DbSet<SF_OrderUnUseDatum> SF_OrderUnUseData { get; set; }

    public virtual DbSet<SF_Repast> SF_Repasts { get; set; }

    public virtual DbSet<SK_ScanCode> SK_ScanCodes { get; set; }

    public virtual DbSet<ST_AlterCard> ST_AlterCards { get; set; }

    public virtual DbSet<ST_AlterCardHistory> ST_AlterCardHistories { get; set; }

    public virtual DbSet<ST_BlackPerson> ST_BlackPeople { get; set; }

    public virtual DbSet<ST_Card> ST_Cards { get; set; }

    public virtual DbSet<ST_CardInfo> ST_CardInfos { get; set; }

    public virtual DbSet<ST_CardType> ST_CardTypes { get; set; }

    public virtual DbSet<ST_ClearDatum> ST_ClearData { get; set; }

    public virtual DbSet<ST_ClearTable> ST_ClearTables { get; set; }

    public virtual DbSet<ST_DeleteCard> ST_DeleteCards { get; set; }

    public virtual DbSet<ST_DeleteCardHistory> ST_DeleteCardHistories { get; set; }

    public virtual DbSet<ST_Department> ST_Departments { get; set; }

    public virtual DbSet<ST_DeptRight> ST_DeptRights { get; set; }

    public virtual DbSet<ST_Dictionary> ST_Dictionaries { get; set; }

    public virtual DbSet<ST_DisableCard> ST_DisableCards { get; set; }

    public virtual DbSet<ST_EnCard> ST_EnCards { get; set; }

    public virtual DbSet<ST_Fingerprint> ST_Fingerprints { get; set; }

    public virtual DbSet<ST_Identity> ST_Identities { get; set; }

    public virtual DbSet<ST_LockCard> ST_LockCards { get; set; }

    public virtual DbSet<ST_MOC> ST_MOCs { get; set; }

    public virtual DbSet<ST_MenuInfo> ST_MenuInfos { get; set; }

    public virtual DbSet<ST_NoBalance> ST_NoBalances { get; set; }

    public virtual DbSet<ST_NoPersonCard> ST_NoPersonCards { get; set; }

    public virtual DbSet<ST_OptLog> ST_OptLogs { get; set; }

    public virtual DbSet<ST_Person> ST_People { get; set; }

    public virtual DbSet<ST_PersonAccount> ST_PersonAccounts { get; set; }

    public virtual DbSet<ST_PersonPhoto> ST_PersonPhotos { get; set; }

    public virtual DbSet<ST_RightInfo> ST_RightInfos { get; set; }

    public virtual DbSet<ST_RightValue> ST_RightValues { get; set; }

    public virtual DbSet<ST_RoleInfo> ST_RoleInfos { get; set; }

    public virtual DbSet<ST_RoleRight> ST_RoleRights { get; set; }

    public virtual DbSet<ST_SelfMenu> ST_SelfMenus { get; set; }

    public virtual DbSet<ST_SubSysRight> ST_SubSysRights { get; set; }

    public virtual DbSet<ST_SubSystem> ST_SubSystems { get; set; }

    public virtual DbSet<ST_Subsidy> ST_Subsidies { get; set; }

    public virtual DbSet<ST_SubsidyAccount> ST_SubsidyAccounts { get; set; }

    public virtual DbSet<ST_SubsidyHistory> ST_SubsidyHistories { get; set; }

    public virtual DbSet<ST_SupplyFund> ST_SupplyFunds { get; set; }

    public virtual DbSet<ST_SupplyFundHistory> ST_SupplyFundHistories { get; set; }

    public virtual DbSet<ST_SysLog> ST_SysLogs { get; set; }

    public virtual DbSet<ST_SysParam> ST_SysParams { get; set; }

    public virtual DbSet<ST_SystemBalance> ST_SystemBalances { get; set; }

    public virtual DbSet<ST_TakeFund> ST_TakeFunds { get; set; }

    public virtual DbSet<ST_TakeFundHistory> ST_TakeFundHistories { get; set; }

    public virtual DbSet<ST_TrafficLog2201> ST_TrafficLog2201s { get; set; }

    public virtual DbSet<ST_TransferAccount> ST_TransferAccounts { get; set; }

    public virtual DbSet<ST_TransferAccountHistory> ST_TransferAccountHistories { get; set; }

    public virtual DbSet<ST_UnLockCard> ST_UnLockCards { get; set; }

    public virtual DbSet<ST_UserInfo> ST_UserInfos { get; set; }

    public virtual DbSet<V_KQAdjustClass> V_KQAdjustClasses { get; set; }

    public virtual DbSet<V_KQAdjustClassHistory> V_KQAdjustClassHistories { get; set; }

    public virtual DbSet<V_KQAdjustRest> V_KQAdjustRests { get; set; }

    public virtual DbSet<V_KQAdjustRestHistory> V_KQAdjustRestHistories { get; set; }

    public virtual DbSet<V_KQAttendInfo> V_KQAttendInfos { get; set; }

    public virtual DbSet<V_KQBrushCard> V_KQBrushCards { get; set; }

    public virtual DbSet<V_KQBrushCardHistory> V_KQBrushCardHistories { get; set; }

    public virtual DbSet<V_KQClassInfo> V_KQClassInfos { get; set; }

    public virtual DbSet<V_KQFillAttend> V_KQFillAttends { get; set; }

    public virtual DbSet<V_KQFillAttendHistory> V_KQFillAttendHistories { get; set; }

    public virtual DbSet<V_KQLeave> V_KQLeaves { get; set; }

    public virtual DbSet<V_KQLeaveHistory> V_KQLeaveHistories { get; set; }

    public virtual DbSet<V_KQOvertime> V_KQOvertimes { get; set; }

    public virtual DbSet<V_KQOvertimeHistory> V_KQOvertimeHistories { get; set; }

    public virtual DbSet<V_KQPerson> V_KQPeople { get; set; }

    public virtual DbSet<V_KQSupplyTime> V_KQSupplyTimes { get; set; }

    public virtual DbSet<V_KQSupplyTimeHistory> V_KQSupplyTimeHistories { get; set; }

    public virtual DbSet<V_MJAbnormRecord> V_MJAbnormRecords { get; set; }

    public virtual DbSet<V_MJAlarmRecord> V_MJAlarmRecords { get; set; }

    public virtual DbSet<V_MJDotLinkage> V_MJDotLinkages { get; set; }

    public virtual DbSet<V_MJMonitorRecord> V_MJMonitorRecords { get; set; }

    public virtual DbSet<V_MJNormalRecord> V_MJNormalRecords { get; set; }

    public virtual DbSet<V_MJPersonInfo> V_MJPersonInfos { get; set; }

    public virtual DbSet<V_MJPersonIntOutTime> V_MJPersonIntOutTimes { get; set; }

    public virtual DbSet<V_STAlterCard> V_STAlterCards { get; set; }

    public virtual DbSet<V_STPerson> V_STPeople { get; set; }

    public virtual DbSet<V_STPersonCard> V_STPersonCards { get; set; }

    public virtual DbSet<V_STSubSidy> V_STSubSidies { get; set; }

    public virtual DbSet<V_XFConsumeCleanup> V_XFConsumeCleanups { get; set; }

    public virtual DbSet<V_XFConsumeFake> V_XFConsumeFakes { get; set; }

    public virtual DbSet<V_XFPsnConsume> V_XFPsnConsumes { get; set; }

    public virtual DbSet<V_XFPsnConsume_Ex> V_XFPsnConsume_Exes { get; set; }

    public virtual DbSet<V_XFSectArea> V_XFSectAreas { get; set; }

    public virtual DbSet<VisitorApply> VisitorApplies { get; set; }

    public virtual DbSet<VisitorEntourage> VisitorEntourages { get; set; }

    public virtual DbSet<VisitorUser> VisitorUsers { get; set; }

    public virtual DbSet<WX_ApiLog> WX_ApiLogs { get; set; }

    public virtual DbSet<WX_Feedback> WX_Feedbacks { get; set; }

    public virtual DbSet<WX_FeedbackImage> WX_FeedbackImages { get; set; }

    public virtual DbSet<WX_KQ_Appeal> WX_KQ_Appeals { get; set; }

    public virtual DbSet<WX_KQ_Apply> WX_KQ_Applies { get; set; }

    public virtual DbSet<WX_Message> WX_Messages { get; set; }

    public virtual DbSet<WX_RechargeLog> WX_RechargeLogs { get; set; }

    public virtual DbSet<WX_SmsCode> WX_SmsCodes { get; set; }

    public virtual DbSet<WX_VisitorCard> WX_VisitorCards { get; set; }

    public virtual DbSet<XF_Area> XF_Areas { get; set; }

    public virtual DbSet<XF_Collect> XF_Collects { get; set; }

    public virtual DbSet<XF_CollectLog> XF_CollectLogs { get; set; }

    public virtual DbSet<XF_Consume> XF_Consumes { get; set; }

    public virtual DbSet<XF_ConsumeCleanup> XF_ConsumeCleanups { get; set; }

    public virtual DbSet<XF_ConsumeDetail> XF_ConsumeDetails { get; set; }

    public virtual DbSet<XF_ConsumeDetailHistory> XF_ConsumeDetailHistories { get; set; }

    public virtual DbSet<XF_ConsumeExcept> XF_ConsumeExcepts { get; set; }

    public virtual DbSet<XF_ConsumeFake> XF_ConsumeFakes { get; set; }

    public virtual DbSet<XF_ConsumeHistory> XF_ConsumeHistories { get; set; }

    public virtual DbSet<XF_ConsumeTemp> XF_ConsumeTemps { get; set; }

    public virtual DbSet<XF_KeyType> XF_KeyTypes { get; set; }

    public virtual DbSet<XF_OrderPending> XF_OrderPendings { get; set; }

    public virtual DbSet<XF_OrderPendingSub> XF_OrderPendingSubs { get; set; }

    public virtual DbSet<XF_PayRecord> XF_PayRecords { get; set; }

    public virtual DbSet<XF_PayRecordTemp> XF_PayRecordTemps { get; set; }

    public virtual DbSet<XF_Section> XF_Sections { get; set; }

    public virtual DbSet<XF_Subsidy> XF_Subsidies { get; set; }

    public virtual DbSet<XF_SysParam> XF_SysParams { get; set; }

    public virtual DbSet<XF_WareInWeek> XF_WareInWeeks { get; set; }

    public virtual DbSet<XF_WareInfo> XF_WareInfos { get; set; }

    public virtual DbSet<XF_WareInfoPhoto> XF_WareInfoPhotos { get; set; }

    public virtual DbSet<XF_WareType> XF_WareTypes { get; set; }

    public virtual DbSet<grade> grades { get; set; }

    public virtual DbSet<school> schools { get; set; }

    public virtual DbSet<st_moc_upload_name_log> st_moc_upload_name_logs { get; set; }

    public virtual DbSet<student_parent> student_parents { get; set; }

    public virtual DbSet<user> users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=182.92.177.60;Database=STCard42Sample;User Id=sa;Password=sa123456_;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DC_OrderDataLog>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__DC_OrderDataLog__0E8E2250");

            entity.ToTable("DC_OrderDataLog");

            entity.Property(e => e.Card_Balance).HasColumnType("money");
            entity.Property(e => e.Consume_Fund).HasColumnType("money");
            entity.Property(e => e.Consume_Time).HasColumnType("datetime");
            entity.Property(e => e.DB_Balance).HasColumnType("money");
            entity.Property(e => e.Del_Date).HasColumnType("datetime");
            entity.Property(e => e.Delivery_Addr)
                .HasMaxLength(256)
                .IsUnicode(false);
            entity.Property(e => e.Download_Time).HasColumnType("datetime");
            entity.Property(e => e.Moc_No_DC)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Moc_No_SF)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Order_BeforeFund).HasColumnType("money");
            entity.Property(e => e.Order_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Order_Fund).HasColumnType("money");
            entity.Property(e => e.Order_No)
                .HasMaxLength(60)
                .IsUnicode(false);
            entity.Property(e => e.Order_Time).HasColumnType("datetime");
            entity.Property(e => e.Remark)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Repast_No)
                .HasMaxLength(3)
                .IsUnicode(false);
            entity.Property(e => e.Taste)
                .HasMaxLength(256)
                .IsUnicode(false);
            entity.Property(e => e.Telephone)
                .HasMaxLength(36)
                .IsUnicode(false);
            entity.Property(e => e.User_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<DC_OrderDataLogSub>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__DC_Order__2DF8989E7FD5EEA5");

            entity.ToTable("DC_OrderDataLogSub");

            entity.Property(e => e.Order_No)
                .HasMaxLength(60)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Ware_BeforeFund).HasColumnType("money");
            entity.Property(e => e.Ware_Copies).HasDefaultValue(1);
            entity.Property(e => e.Ware_Fund).HasColumnType("money");
            entity.Property(e => e.Ware_Price).HasColumnType("money");
            entity.Property(e => e.Ware_Taste)
                .HasMaxLength(256)
                .IsUnicode(false);
        });

        modelBuilder.Entity<DC_OrderDataSub>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__DC_Order__2DF8989E7834CCDD");

            entity.ToTable("DC_OrderDataSub");

            entity.Property(e => e.Order_No)
                .HasMaxLength(60)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Ware_BeforeFund).HasColumnType("money");
            entity.Property(e => e.Ware_Copies).HasDefaultValue(1);
            entity.Property(e => e.Ware_Fund).HasColumnType("money");
            entity.Property(e => e.Ware_Price).HasColumnType("money");
            entity.Property(e => e.Ware_Taste)
                .HasMaxLength(256)
                .IsUnicode(false);
        });

        modelBuilder.Entity<DC_OrderDatum>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__DC_OrderData__0BB1B5A5");

            entity.HasIndex(e => new { e.Person_ID, e.Order_Date, e.Repast_No }, "IX_DC_OrderData");

            entity.Property(e => e.Consume_Time).HasColumnType("datetime");
            entity.Property(e => e.Delivery_Addr)
                .HasMaxLength(256)
                .IsUnicode(false);
            entity.Property(e => e.Download_Time).HasColumnType("datetime");
            entity.Property(e => e.Moc_No_DC)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Moc_No_SF)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Order_BeforeFund).HasColumnType("money");
            entity.Property(e => e.Order_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Order_Fund).HasColumnType("money");
            entity.Property(e => e.Order_No)
                .HasMaxLength(60)
                .IsUnicode(false);
            entity.Property(e => e.Order_Time).HasColumnType("datetime");
            entity.Property(e => e.Remark)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Repast_No)
                .HasMaxLength(3)
                .IsUnicode(false);
            entity.Property(e => e.Taste)
                .HasMaxLength(256)
                .IsUnicode(false);
            entity.Property(e => e.Telephone)
                .HasMaxLength(36)
                .IsUnicode(false);
        });

        modelBuilder.Entity<DC_Repast>(entity =>
        {
            entity.HasKey(e => e.Repast_No).HasName("PK__DC_Repast__07E124C1");

            entity.ToTable("DC_Repast");

            entity.Property(e => e.Repast_No)
                .HasMaxLength(3)
                .IsUnicode(false);
            entity.Property(e => e.Bgn_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Can_Order).HasDefaultValue(true);
            entity.Property(e => e.End_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
        });

        modelBuilder.Entity<FK_SysParam>(entity =>
        {
            entity.HasKey(e => e.Param_ID).HasName("PK__FK_SysPa__C4B2845853C2623D");

            entity.ToTable("FK_SysParam");

            entity.Property(e => e.Param_Code)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Param_Name)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Param_Value)
                .HasMaxLength(300)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_Account>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_Account__3552E9B6");

            entity.ToTable("KQ_Account");

            entity.HasIndex(e => e.Account_Code, "UQ__KQ_Account__36470DEF").IsUnique();

            entity.Property(e => e.Account_Code)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Account_Date).HasColumnType("datetime");
            entity.Property(e => e.Account_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Date_Bgn)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Date_End)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_AdjustClass>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_AdjustClass__65F62111");

            entity.ToTable("KQ_AdjustClass");

            entity.HasIndex(e => e.Adjust_Date, "KQ_AdjustClass_DateIndex");

            entity.HasIndex(e => new { e.Person_ID, e.Adjust_Date }, "KQ_AdjustClass_QueryIndex");

            entity.Property(e => e.Adjust_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Class_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_AdjustClassHistory>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_AdjustClassHi__28B808A7");

            entity.ToTable("KQ_AdjustClassHistory");

            entity.HasIndex(e => e.Adjust_Date, "KQ_AdjustClassHistory_DateIndex");

            entity.Property(e => e.Adjust_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Class_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_AdjustRest>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_AdjustRest__5A846E65");

            entity.ToTable("KQ_AdjustRest");

            entity.HasIndex(e => e.Adjust_Date, "KQ_AdjustRest_DateIndex");

            entity.HasIndex(e => new { e.Adjust_Class, e.Person_ID, e.Adjust_Date }, "KQ_AdjustRest_QueryIndex");

            entity.Property(e => e.Adjust_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_AdjustRestHistory>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_AdjustRestHis__2116E6DF");

            entity.ToTable("KQ_AdjustRestHistory");

            entity.HasIndex(e => e.Adjust_Date, "KQ_AdjustRestHistory_DateIndex");

            entity.Property(e => e.Adjust_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_AgileClass>(entity =>
        {
            entity.HasKey(e => e.Class_No).HasName("PK__KQ_AgileClass__0D0FEE32");

            entity.ToTable("KQ_AgileClass");

            entity.Property(e => e.Class_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Class_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_AgileClassSub>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_AgileClassSub__0FEC5ADD");

            entity.ToTable("KQ_AgileClassSub");

            entity.HasIndex(e => e.Class_No, "XIF1KQ_AgileClassSub");

            entity.Property(e => e.Class_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Shift_ClassNo)
                .HasMaxLength(6)
                .IsUnicode(false);

            entity.HasOne(d => d.Class_NoNavigation).WithMany(p => p.KQ_AgileClassSubs)
                .HasForeignKey(d => d.Class_No)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__KQ_AgileC__Class__10E07F16");
        });

        modelBuilder.Entity<KQ_BrushCard>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_BrushCard__3A179ED3");

            entity.ToTable("KQ_BrushCard");

            entity.HasIndex(e => new { e.Person_ID, e.Brush_Date }, "BrushCardIndex");

            entity.HasIndex(e => e.Brush_Date, "KQ_BrushCard_DateIndex");

            entity.Property(e => e.Brush_Data)
                .HasMaxLength(256)
                .IsUnicode(false);
            entity.Property(e => e.Brush_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_BrushCardHistory>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_BrushCardHist__2E70E1FD");

            entity.ToTable("KQ_BrushCardHistory");

            entity.HasIndex(e => e.Brush_Date, "KQ_BrushCardHistory_DateIndex");

            entity.Property(e => e.Brush_Data)
                .HasMaxLength(256)
                .IsUnicode(false);
            entity.Property(e => e.Brush_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_Class>(entity =>
        {
            entity.HasKey(e => e.Class_No).HasName("PK__KQ_Class__68D28DBC");

            entity.ToTable("KQ_Class");

            entity.Property(e => e.Class_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Class_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Sect1_After).HasDefaultValue((short)0);
            entity.Property(e => e.Sect1_Ahead).HasDefaultValue((short)0);
            entity.Property(e => e.Sect1_Deduct).HasDefaultValue((short)0);
            entity.Property(e => e.Sect1_InCard).HasDefaultValue((byte)1);
            entity.Property(e => e.Sect1_InTime)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Sect1_OutCard).HasDefaultValue((byte)1);
            entity.Property(e => e.Sect1_OutTime)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Sect2_After).HasDefaultValue((short)0);
            entity.Property(e => e.Sect2_Ahead).HasDefaultValue((short)0);
            entity.Property(e => e.Sect2_Deduct).HasDefaultValue((short)0);
            entity.Property(e => e.Sect2_InCard).HasDefaultValue((byte)1);
            entity.Property(e => e.Sect2_InTime)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Sect2_OutCard).HasDefaultValue((byte)1);
            entity.Property(e => e.Sect2_OutTime)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Sect3_After).HasDefaultValue((short)0);
            entity.Property(e => e.Sect3_Ahead).HasDefaultValue((short)0);
            entity.Property(e => e.Sect3_Deduct).HasDefaultValue((short)0);
            entity.Property(e => e.Sect3_InCard).HasDefaultValue((byte)1);
            entity.Property(e => e.Sect3_InTime)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Sect3_OutCard).HasDefaultValue((byte)1);
            entity.Property(e => e.Sect3_OutTime)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Sect4_After).HasDefaultValue((short)0);
            entity.Property(e => e.Sect4_Ahead).HasDefaultValue((short)0);
            entity.Property(e => e.Sect4_Deduct).HasDefaultValue((short)0);
            entity.Property(e => e.Sect4_InCard).HasDefaultValue((byte)1);
            entity.Property(e => e.Sect4_InTime)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Sect4_OutCard).HasDefaultValue((byte)1);
            entity.Property(e => e.Sect4_OutTime)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Sect5_After).HasDefaultValue((short)0);
            entity.Property(e => e.Sect5_Ahead).HasDefaultValue((short)0);
            entity.Property(e => e.Sect5_Deduct).HasDefaultValue((short)0);
            entity.Property(e => e.Sect5_InCard).HasDefaultValue((byte)1);
            entity.Property(e => e.Sect5_InTime)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Sect5_OutCard).HasDefaultValue((byte)1);
            entity.Property(e => e.Sect5_OutTime)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_Daily>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_Daily__3BFFE745");

            entity.ToTable("KQ_Daily");

            entity.HasIndex(e => e.Worktime_Date, "KQ_Daily_DateIndex");

            entity.Property(e => e.Brush_Data)
                .HasMaxLength(256)
                .IsUnicode(false);
            entity.Property(e => e.Class_Data)
                .HasMaxLength(256)
                .IsUnicode(false);
            entity.Property(e => e.Class_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Early_Data)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Late_Data)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Leave_Data)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Leave_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.OfWeek)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.Overtime_Class)
                .HasMaxLength(1)
                .IsUnicode(false);
            entity.Property(e => e.Reason)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Sect1_InFact)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.Sect1_OutFact)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.Sect2_InFact)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.Sect2_OutFact)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.Sect3_InFact)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.Sect3_OutFact)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.Sect4_InFact)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.Sect4_OutFact)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.Sect5_InFact)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.Sect5_OutFact)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.Worktime_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_DailyHistory>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_DailyHistory__30592A6F");

            entity.ToTable("KQ_DailyHistory");

            entity.HasIndex(e => e.Worktime_Date, "KQ_DailyHistory_DateIndex");

            entity.Property(e => e.Brush_Data)
                .HasMaxLength(256)
                .IsUnicode(false);
            entity.Property(e => e.Class_Data)
                .HasMaxLength(256)
                .IsUnicode(false);
            entity.Property(e => e.Class_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Early_Data)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Late_Data)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Leave_Data)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Leave_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.OfWeek)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.Overtime_Class)
                .HasMaxLength(1)
                .IsUnicode(false);
            entity.Property(e => e.Reason)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Sect1_InFact)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.Sect1_OutFact)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.Sect2_InFact)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.Sect2_OutFact)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.Sect3_InFact)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.Sect3_OutFact)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.Sect4_InFact)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.Sect4_OutFact)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.Sect5_InFact)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.Sect5_OutFact)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.Worktime_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_DefineClass>(entity =>
        {
            entity.HasKey(e => e.Class_No).HasName("PK__KQ_DefineClass__2CBDA3B5");

            entity.ToTable("KQ_DefineClass");

            entity.Property(e => e.Class_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Class_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_DefineClassSub>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_DefineClassSu__2F9A1060");

            entity.ToTable("KQ_DefineClassSub");

            entity.HasIndex(e => e.Worktime_Date, "KQ_DefineClassSub_DateIndex");

            entity.HasIndex(e => e.Class_No, "XIF1KQ_DefineClassSub");

            entity.Property(e => e.Class_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Day_ClassNo)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.OfWeek)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.Worktime_Date)
                .HasMaxLength(10)
                .IsUnicode(false);

            entity.HasOne(d => d.Class_NoNavigation).WithMany(p => p.KQ_DefineClassSubs)
                .HasForeignKey(d => d.Class_No)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__KQ_Define__Class__308E3499");
        });

        modelBuilder.Entity<KQ_DefineClassSubHistory>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_DefineClassSu__14B10FFA");

            entity.ToTable("KQ_DefineClassSubHistory");

            entity.HasIndex(e => e.Worktime_Date, "KQ_DefineClassSubHistory_DateIndex");

            entity.Property(e => e.Class_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Day_ClassNo)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.OfWeek)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.Worktime_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_Download>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_Download__32767D0B");

            entity.ToTable("KQ_Download");

            entity.HasIndex(e => new { e.Card_No, e.Brush_Date }, "IX_KQ_Download_CardNo_Date");

            entity.HasIndex(e => e.Brush_Date, "IX_KQ_Download_Date");

            entity.HasIndex(e => new { e.Person_ID, e.Brush_Date }, "IX_KQ_Download_PID_Date");

            entity.HasIndex(e => new { e.Person_ID, e.Brush_DateTime }, "IX_KQ_Download_PID_DateTime");

            entity.Property(e => e.Brush_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Brush_DateTime).HasColumnType("datetime");
            entity.Property(e => e.Brush_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.FileName)
                .HasMaxLength(128)
                .IsUnicode(false);
            entity.Property(e => e.Is_Falsity).HasDefaultValue(false);
            entity.Property(e => e.Moc_No)
                .HasMaxLength(3)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_DownloadHistory>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_DownloadHisto__2B947552");

            entity.ToTable("KQ_DownloadHistory");

            entity.HasIndex(e => new { e.Card_No, e.Brush_Date, e.Brush_Time }, "IX_KQ_DownloadHistory");

            entity.HasIndex(e => e.Brush_Date, "IX_KQ_DownloadHistory_Date");

            entity.Property(e => e.Brush_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Brush_DateTime).HasColumnType("datetime");
            entity.Property(e => e.Brush_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.FileName)
                .HasMaxLength(128)
                .IsUnicode(false);
            entity.Property(e => e.Is_Falsity).HasDefaultValue(false);
            entity.Property(e => e.Moc_No)
                .HasMaxLength(3)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_FillAttend>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_FillAttend__6225902D");

            entity.ToTable("KQ_FillAttend");

            entity.HasIndex(e => e.Fill_Date, "KQ_FillAttend_DateIndex");

            entity.HasIndex(e => new { e.Fill_Class, e.Person_ID, e.Fill_Date }, "KQ_FillAttend_QueryIndex");

            entity.Property(e => e.Fill_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_FillAttendHistory>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_FillAttendHis__24E777C3");

            entity.ToTable("KQ_FillAttendHistory");

            entity.HasIndex(e => e.Fill_Date, "KQ_FillAttendHistory_DateIndex");

            entity.Property(e => e.Fill_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_Holiday>(entity =>
        {
            entity.HasKey(e => e.Holiday_Date).HasName("PK__KQ_Holiday__51EF2864");

            entity.ToTable("KQ_Holiday");

            entity.Property(e => e.Holiday_Date)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Holiday_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_InOutDataHistory>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_InOutDataHist__324172E1");

            entity.ToTable("KQ_InOutDataHistory");

            entity.HasIndex(e => e.Brush_Date, "KQ_InOutDataHistory_DateIndex");

            entity.Property(e => e.Brush_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Data_In)
                .HasMaxLength(256)
                .IsUnicode(false);
            entity.Property(e => e.Data_Out)
                .HasMaxLength(256)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_InOutDatum>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_InOutData__477199F1");

            entity.HasIndex(e => e.Brush_Date, "KQ_InOutData_DateIndex");

            entity.Property(e => e.Brush_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Data_In)
                .HasMaxLength(256)
                .IsUnicode(false);
            entity.Property(e => e.Data_Out)
                .HasMaxLength(256)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_Leave>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_Leave__40C49C62");

            entity.ToTable("KQ_Leave");

            entity.HasIndex(e => e.Leave_Date, "KQ_Leave_DateIndex");

            entity.HasIndex(e => new { e.Person_ID, e.Leave_Date }, "KQ_Leave_QueryIndex");

            entity.HasIndex(e => e.Kind_No, "XIF1KQ_Leave");

            entity.Property(e => e.Kind_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Leave_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Leave_TimeBgn)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Leave_TimeEnd)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Kind_NoNavigation).WithMany(p => p.KQ_Leaves)
                .HasForeignKey(d => d.Kind_No)
                .HasConstraintName("FK__KQ_Leave__Kind_N__43A1090D");
        });

        modelBuilder.Entity<KQ_LeaveHistory>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_LeaveHistory__1A69E950");

            entity.ToTable("KQ_LeaveHistory");

            entity.HasIndex(e => e.Leave_Date, "KQ_LeaveHistory_DateIndex");

            entity.Property(e => e.Kind_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Leave_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Leave_TimeBgn)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Leave_TimeEnd)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_LeaveKind>(entity =>
        {
            entity.HasKey(e => e.Kind_No).HasName("PK__KQ_LeaveKind__3DE82FB7");

            entity.ToTable("KQ_LeaveKind");

            entity.Property(e => e.Kind_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Is_Deduct).HasDefaultValue(true);
            entity.Property(e => e.Kind_Code)
                .HasMaxLength(3)
                .IsUnicode(false);
            entity.Property(e => e.Kind_Name)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_ModifyMonthly>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_ModifyMonthly__361203C5");

            entity.ToTable("KQ_ModifyMonthly");

            entity.Property(e => e.Account_Code)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_ModifyMonthlyHistory>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_ModifyMonthly__37FA4C37");

            entity.ToTable("KQ_ModifyMonthlyHistory");

            entity.Property(e => e.Account_Code)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_Monthly>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_Monthly__2AD55B43");

            entity.ToTable("KQ_Monthly");

            entity.HasIndex(e => e.Account_Code, "KQ_Monthly_DateIndex");

            entity.Property(e => e.Account_Code)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_MonthlyHistory>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_MonthlyHistor__3429BB53");

            entity.ToTable("KQ_MonthlyHistory");

            entity.HasIndex(e => e.Account_Code, "KQ_MonthlyHistory_DateIndex");

            entity.Property(e => e.Account_Code)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_Overtime>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_Overtime__53D770D6");

            entity.ToTable("KQ_Overtime");

            entity.HasIndex(e => e.Over_Date, "KQ_Overtime_DateIndex");

            entity.HasIndex(e => new { e.Person_ID, e.Over_Date }, "KQ_Overtime_QueryIndex");

            entity.Property(e => e.Over_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_OvertimeHistory>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_OvertimeHisto__1E3A7A34");

            entity.ToTable("KQ_OvertimeHistory");

            entity.HasIndex(e => e.Over_Date, "KQ_OvertimeHistory_DateIndex");

            entity.Property(e => e.Over_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_Person>(entity =>
        {
            entity.HasKey(e => e.Person_ID).HasName("PK__KQ_Person__4959E263");

            entity.ToTable("KQ_Person");

            entity.Property(e => e.Person_ID).ValueGeneratedNever();
            entity.Property(e => e.Class_No)
                .HasMaxLength(6)
                .IsUnicode(false)
                .HasDefaultValue("");
            entity.Property(e => e.IsHolidayAsWorkTime).HasDefaultValue(false);
            entity.Property(e => e.IsOverTimeAsWorkTime).HasDefaultValue(false);
            entity.Property(e => e.Overtime_Mode).HasDefaultValue((byte)1);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Rest_Days)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Worktime_Mode).HasDefaultValue((byte)1);
        });

        modelBuilder.Entity<KQ_RealInfo>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_RealI__2DF8989E05C3D225");

            entity.ToTable("KQ_RealInfo");

            entity.Property(e => e.Brush_Time).HasColumnType("datetime");
            entity.Property(e => e.Card_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.IP_Addr)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Real_Image).HasColumnType("image");
            entity.Property(e => e.Update_Time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
        });

        modelBuilder.Entity<KQ_RuleClass>(entity =>
        {
            entity.HasKey(e => e.Class_No).HasName("PK__KQ_RuleClass__56B3DD81");

            entity.ToTable("KQ_RuleClass");

            entity.Property(e => e.Class_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Class_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Rule_Mode).HasDefaultValue((byte)0);
            entity.Property(e => e.Start_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_RuleClassSub>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_RuleClassSub__0A338187");

            entity.ToTable("KQ_RuleClassSub");

            entity.HasIndex(e => e.Class_No, "XIF1KQ_RuleClassSub");

            entity.Property(e => e.Class_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Shift_ClassNo)
                .HasMaxLength(6)
                .IsUnicode(false);

            entity.HasOne(d => d.Class_NoNavigation).WithMany(p => p.KQ_RuleClassSubs)
                .HasForeignKey(d => d.Class_No)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__KQ_RuleCl__Class__0B27A5C0");
        });

        modelBuilder.Entity<KQ_SupplyTime>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_SupplyTime__5E54FF49");

            entity.ToTable("KQ_SupplyTime");

            entity.HasIndex(e => e.Supply_Date, "KQ_SupplyTime_DateIndex");

            entity.HasIndex(e => new { e.Person_ID, e.Supply_Date }, "KQ_SupplyTime_QueryIndex");

            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Supply_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Supply_Reason)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Supply_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_SupplyTimeHistory>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_SupplyTimeHis__1699586C");

            entity.ToTable("KQ_SupplyTimeHistory");

            entity.HasIndex(e => e.Supply_Date, "KQ_SupplyTimeHistory_DateIndex");

            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Supply_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Supply_Reason)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Supply_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_SysParam>(entity =>
        {
            entity.HasKey(e => e.Param_ID).HasName("PK__KQ_SysParam__084B3915");

            entity.ToTable("KQ_SysParam");

            entity.Property(e => e.Param_Code)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Param_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Param_Value)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_TransferTable>(entity =>
        {
            entity.HasKey(e => e.Table_Name).HasName("PK__KQ_TransferTable__12C8C788");

            entity.Property(e => e.Table_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Date_Field)
                .HasMaxLength(40)
                .IsUnicode(false);
        });

        modelBuilder.Entity<KQ_Yearly>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__KQ_Yearly__4589517F");

            entity.ToTable("KQ_Yearly");

            entity.HasIndex(e => e.Account_Code, "KQ_Yearly_DateIndex");

            entity.Property(e => e.Account_Code)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<LH_DiscountKind>(entity =>
        {
            entity.HasKey(e => e.Discount_Kind).HasName("PK__LH_Disco__92C4D4743EFC4F81");

            entity.ToTable("LH_DiscountKind");

            entity.Property(e => e.Default_ConsumeFund).HasColumnType("money");
            entity.Property(e => e.Discount_Name)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Discount_Option).HasDefaultValue(0);
            entity.Property(e => e.LeastConsume_Fund).HasColumnType("money");
            entity.Property(e => e.Remark)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<LH_FeastDiscount>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__LH_Feast__2DF8989E4F32B74A");

            entity.ToTable("LH_FeastDiscount");

            entity.Property(e => e.Feast_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<LH_GetConsume>(entity =>
        {
            entity.HasKey(e => e.ID_Key).HasName("PK__LH_GetCo__2DF994E63B2BBE9D");

            entity.ToTable("LH_GetConsume");

            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Consume_AllFund).HasColumnType("money");
            entity.Property(e => e.Get_Time).HasColumnType("datetime");
            entity.Property(e => e.Remark)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.User_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<LH_MinuteDiscount>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__LH_Minut__2DF8989E47919582");

            entity.ToTable("LH_MinuteDiscount");

            entity.Property(e => e.Remark)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<LH_SetParamCard>(entity =>
        {
            entity.HasKey(e => e.Mny_Index).HasName("PK__LH_SetPa__A6EAAF7B5AA469F6");

            entity.ToTable("LH_SetParamCard");

            entity.Property(e => e.Bgn_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.End_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Unit_Fund).HasColumnType("money");
        });

        modelBuilder.Entity<LH_TimeBrushCard>(entity =>
        {
            entity.HasKey(e => e.Card_No).HasName("PK__LH_TimeB__45AA6CB056D3D912");

            entity.ToTable("LH_TimeBrushCard");

            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Brush_Time).HasColumnType("datetime");
        });

        modelBuilder.Entity<LH_TimeLenDiscount>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__LH_TimeL__2DF8989E4B622666");

            entity.ToTable("LH_TimeLenDiscount");

            entity.Property(e => e.Remark)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<LH_TimePartDiscount>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__LH_TimeP__2DF8989E43C1049E");

            entity.ToTable("LH_TimePartDiscount");

            entity.Property(e => e.Bgn_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.End_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<LH_WeekDiscount>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__LH_WeekD__2DF8989E5303482E");

            entity.ToTable("LH_WeekDiscount");

            entity.Property(e => e.Remark)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_AbnormRecord>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__MJ_AbnormRecord__7E8CC4B1");

            entity.ToTable("MJ_AbnormRecord");

            entity.HasIndex(e => e.BrushCard_Time, "IX_MJAbnormRecord_BrushCardTime");

            entity.Property(e => e.BrushCard_Time).HasColumnType("datetime");
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Door_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Is_InDoor).HasDefaultValue(true);
        });

        modelBuilder.Entity<MJ_AlarmOut>(entity =>
        {
            entity.HasKey(e => new { e.Ctrl_No, e.Idx_No }).HasName("PK__MJ_AlarmOut__3B95D2F1");

            entity.ToTable("MJ_AlarmOut");

            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_AlarmRecord>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__MJ_AlarmRecord__7BB05806");

            entity.ToTable("MJ_AlarmRecord");

            entity.HasIndex(e => e.Alarm_Time, "IX_MJAlarmRecord_AlarmTime");

            entity.Property(e => e.Alarm_Time).HasColumnType("datetime");
            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Dot_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Is_InDoor).HasDefaultValue(true);
        });

        modelBuilder.Entity<MJ_CardManager>(entity =>
        {
            entity.HasKey(e => new { e.Ctrl_No, e.Door_No, e.Person_ID }).HasName("PK__MJ_CardManager__5649C92D");

            entity.ToTable("MJ_CardManager");

            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_ChangeHistory>(entity =>
        {
            entity.HasKey(e => e.ID_Key).HasName("PK__MJ_ChangeHistory__5A1A5A11");

            entity.ToTable("MJ_ChangeHistory");

            entity.Property(e => e.Change_Ctrl)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Change_Date).HasColumnType("datetime");
            entity.Property(e => e.Change_Value)
                .HasMaxLength(40)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_ControlInfo>(entity =>
        {
            entity.HasKey(e => new { e.Ctrl_No, e.Door_No }).HasName("PK__MJ_ControlInfo__5090EFD7");

            entity.ToTable("MJ_ControlInfo", tb => tb.HasTrigger("MJ_tg_ControlInfoMdy"));

            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Alarm_Info)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.Card_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Description)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Run_Info)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.Run_Time).HasColumnType("datetime");
            entity.Property(e => e.State_Info)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Update_Time).HasColumnType("datetime");
        });

        modelBuilder.Entity<MJ_ControlInfoTemp>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__MJ_ControlInfoTe__658C0CBD");

            entity.ToTable("MJ_ControlInfoTemp");

            entity.Property(e => e.Alarm_Info)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.Card_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Description)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Run_Info)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.Run_Time).HasColumnType("datetime");
            entity.Property(e => e.State_Info)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Update_Time).HasColumnType("datetime");
        });

        modelBuilder.Entity<MJ_Controller>(entity =>
        {
            entity.HasKey(e => e.Ctrl_No).HasName("PK__MJ_Controller__1940BAED");

            entity.ToTable("MJ_Controller", tb => tb.HasTrigger("MJ_ControllerWXUpdate"));

            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Com_Addr)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Comm_Content)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.Ctrl_LargeType).HasDefaultValue(0);
            entity.Property(e => e.Ctrl_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Ctrl_Type)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Ctrl_WGType)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("wg34");
            entity.Property(e => e.Download_Time).HasColumnType("datetime");
            entity.Property(e => e.IP_Addr)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.IsLicense).HasDefaultValue(false);
            entity.Property(e => e.Is_Online).HasDefaultValue(false);
            entity.Property(e => e.Is_Up).HasDefaultValue(false);
            entity.Property(e => e.LastOnLineTime).HasColumnType("datetime");
            entity.Property(e => e.LastUpload)
                .HasDefaultValue(new DateTime(2007, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified))
                .HasColumnType("datetime");
            entity.Property(e => e.License_Code)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.Manager_Pwd)
                .HasMaxLength(23)
                .IsUnicode(false)
                .HasDefaultValue("1234");
            entity.Property(e => e.Serial_Num)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.Users_No)
                .HasMaxLength(2100)
                .IsUnicode(false)
                .HasDefaultValue("");
            entity.Property(e => e.Video_IP)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_CtrlDot>(entity =>
        {
            entity.HasKey(e => new { e.Ctrl_No, e.Dot_No }).HasName("PK__MJ_CtrlDot__35DCF99B");

            entity.ToTable("MJ_CtrlDot");

            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Dot_No)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Dot_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Is_Auto).HasDefaultValue(true);
            entity.Property(e => e.Is_Extend).HasDefaultValue(false);
            entity.Property(e => e.Is_Input).HasDefaultValue(false);
        });

        modelBuilder.Entity<MJ_DVSVideoInfo>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__MJ_DVSVideoInfo__63A3C44B");

            entity.ToTable("MJ_DVSVideoInfo");

            entity.HasIndex(e => e.Brush_Date, "IX_MJ_DVSVideoInfoBrushDate");

            entity.HasIndex(e => e.Brush_Time, "IX_MJ_DVSVideoInfoBrushTime");

            entity.HasIndex(e => e.Card_No, "IX_MJ_DVSVideoInfoCardNo");

            entity.Property(e => e.Brush_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Brush_Time)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.Card_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.File_Name)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.File_Path)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_Door>(entity =>
        {
            entity.HasKey(e => new { e.Ctrl_No, e.Door_No }).HasName("PK__MJ_Door__21D600EE");

            entity.ToTable("MJ_Door", tb =>
            {
                tb.HasTrigger("MJ_DoorWXUpdate");
                tb.HasTrigger("MJ_tg_DoorMeetingMode");
            });

            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.APB_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Card_Number).HasDefaultValue((byte)1);
            entity.Property(e => e.DVSServerIP)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.DoorClose_TimeLen).HasDefaultValue((byte)3);
            entity.Property(e => e.DoorLock_TimeLen).HasDefaultValue((byte)1);
            entity.Property(e => e.Door_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.HideBack_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.IP)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.IsVis).HasDefaultValue(false);
            entity.Property(e => e.Is_Up).HasDefaultValue(false);
            entity.Property(e => e.MeetingMode).HasDefaultValue(0);
            entity.Property(e => e.Menace_Pwd)
                .HasMaxLength(5)
                .IsUnicode(false)
                .HasDefaultValueSql("((99999))");
            entity.Property(e => e.OpenAlarm_TimeLen).HasDefaultValue((byte)5);
            entity.Property(e => e.effEDate)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.effSDate)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.idtypepass)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.passids)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.tips)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_DoorMultiCard>(entity =>
        {
            entity.HasKey(e => new { e.Ctrl_No, e.Door_No, e.Value_Type }).HasName("PK__MJ_DoorMultiCard__546180BB");

            entity.ToTable("MJ_DoorMultiCard");

            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Value_Type)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Value)
                .HasMaxLength(12)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_DoorSpecDate>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__MJ_DoorSpecDate__0EC32C7A");

            entity.ToTable("MJ_DoorSpecDate");

            entity.Property(e => e.Spec_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_DoorSpecTime>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__MJ_DoorSpecTime__4AD81681");

            entity.ToTable("MJ_DoorSpecTime");

            entity.Property(e => e.End_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Start_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_DoorSpecWeek>(entity =>
        {
            entity.HasKey(e => e.SpecTime_No).HasName("PK__MJ_DoorSpecWeek__0CDAE408");

            entity.ToTable("MJ_DoorSpecWeek");

            entity.Property(e => e.SpecTime_No).ValueGeneratedNever();
        });

        modelBuilder.Entity<MJ_DoorSwitchTime>(entity =>
        {
            entity.HasKey(e => e.DoorTime_No).HasName("PK__MJ_DoorSwitchTim__4707859D");

            entity.ToTable("MJ_DoorSwitchTime");

            entity.Property(e => e.DoorTime_No).ValueGeneratedNever();
            entity.Property(e => e.DoorTime_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_DoorSwitchTimeSub>(entity =>
        {
            entity.HasKey(e => new { e.DoorTime_No, e.Start_Time, e.End_Time }).HasName("PK__MJ_DoorSwitchTim__48EFCE0F");

            entity.ToTable("MJ_DoorSwitchTimeSub");

            entity.Property(e => e.Start_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.End_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_DotLinkage>(entity =>
        {
            entity.HasKey(e => new { e.Ctrl_No, e.Linkage_No, e.Dot_No }).HasName("PK__MJ_DotLinkage__3D7E1B63");

            entity.ToTable("MJ_DotLinkage");

            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Linkage_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Dot_No)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Linkage_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_EventInfo>(entity =>
        {
            entity.HasKey(e => new { e.Event_Type, e.Event_Code }).HasName("PK__MJ_EventInfo__5832119F");

            entity.ToTable("MJ_EventInfo", tb => tb.HasTrigger("MJ_EventInfoWXUpdate"));

            entity.Property(e => e.Event_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Info_Style)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Is_Up).HasDefaultValue(false);
        });

        modelBuilder.Entity<MJ_ExtendFunc>(entity =>
        {
            entity.HasKey(e => e.Menu_No).HasName("PK__MJ_ExtendFunc__4CC05EF3");

            entity.ToTable("MJ_ExtendFunc");

            entity.Property(e => e.Menu_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Is_Menu).HasDefaultValue(false);
            entity.Property(e => e.Menu_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_FPDoorTimeMerger>(entity =>
        {
            entity.HasKey(e => new { e.Ctrl_No, e.Door_No }).HasName("PK__MJ_FPDoo__71333DD27C3A67EB");

            entity.ToTable("MJ_FPDoorTimeMerger");

            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_FPPsnDoorRight>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__MJ_FPPsn__2DF8989E000AF8CF");

            entity.ToTable("MJ_FPPsnDoorRight");

            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.FP_Pwd)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_FPTime>(entity =>
        {
            entity.HasKey(e => e.Time_No).HasName("PK__MJ_FPTim__5D2D1BF56FD49106");

            entity.ToTable("MJ_FPTime");

            entity.Property(e => e.Time_No).ValueGeneratedNever();
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Time_Name)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_FPTimeDetail>(entity =>
        {
            entity.HasKey(e => new { e.Time_No, e.Week_No }).HasName("PK__MJ_FPTim__B1F7CA5C73A521EA");

            entity.ToTable("MJ_FPTimeDetail");

            entity.Property(e => e.Bgn_Time).HasColumnType("datetime");
            entity.Property(e => e.End_Time).HasColumnType("datetime");
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_FPTimeGroup>(entity =>
        {
            entity.HasKey(e => e.TimeGroup_No).HasName("PK__MJ_FPTim__B021BE197775B2CE");

            entity.ToTable("MJ_FPTimeGroup");

            entity.Property(e => e.TimeGroup_No).ValueGeneratedNever();
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.TimeGroup_Name)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_FPTimeMerger>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("MJ_FPTimeMerger");

            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.TimeMerger_Name)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_Invitation>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__MJ_Invit__2DF8989E6AA5C795");

            entity.ToTable("MJ_Invitation", tb => tb.HasTrigger("MJ_InvitationWXUpdate"));

            entity.Property(e => e.CreateTime).HasColumnType("datetime");
            entity.Property(e => e.EnterpriseNo)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.ExamineTime).HasColumnType("datetime");
            entity.Property(e => e.Extend1)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.InviterDeptName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.InviterName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.InviterNo)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.Is_Up).HasDefaultValue(false);
            entity.Property(e => e.ModifyTime).HasColumnType("datetime");
            entity.Property(e => e.OpenID)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.PlaceInvitation)
                .HasMaxLength(1000)
                .IsUnicode(false);
            entity.Property(e => e.QRCode)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.ReasonVisit)
                .HasMaxLength(1000)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(1000)
                .IsUnicode(false);
            entity.Property(e => e.ReservationDate)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.SignInTime).HasColumnType("datetime");
            entity.Property(e => e.SignOutTime).HasColumnType("datetime");
            entity.Property(e => e.VisitorCode)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.VisitorsName)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_MapDot>(entity =>
        {
            entity.HasKey(e => new { e.Map_No, e.Ctrl_No, e.Door_No }).HasName("PK__MJ_MapDot__451F3D2B");

            entity.ToTable("MJ_MapDot");

            entity.Property(e => e.Map_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_MapInfo>(entity =>
        {
            entity.HasKey(e => e.Map_No).HasName("PK__MJ_MapInfo__4336F4B9");

            entity.ToTable("MJ_MapInfo");

            entity.Property(e => e.Map_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Map_Image).HasColumnType("image");
            entity.Property(e => e.Map_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_MeetingInfo>(entity =>
        {
            entity.HasKey(e => e.ID_Key).HasName("PK__MJ_MeetingInfo__5DEAEAF5");

            entity.ToTable("MJ_MeetingInfo");

            entity.Property(e => e.Bgn_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Bgn_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Door_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.End_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.End_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(40)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_MeetingPerson>(entity =>
        {
            entity.HasKey(e => e.ID_Key).HasName("PK__MJ_MeetingPerson__5FD33367");

            entity.ToTable("MJ_MeetingPerson");

            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Door_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_NormalRecord>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__MJ_NormalRecord__0169315C");

            entity.ToTable("MJ_NormalRecord");

            entity.HasIndex(e => e.BrushCard_Time, "IX_MJNormalRecord_BrushCardTime");

            entity.Property(e => e.BrushCard_Time).HasColumnType("datetime");
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Door_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Is_InDoor).HasDefaultValue(true);
            entity.Property(e => e.bReaded).HasDefaultValue(0);
        });

        modelBuilder.Entity<MJ_Person>(entity =>
        {
            entity.HasKey(e => e.Person_ID).HasName("PK__MJ_Person__10AB74EC");

            entity.ToTable("MJ_Person", tb => tb.HasTrigger("MJ_tg_SyncPerson"));

            entity.Property(e => e.Person_ID).ValueGeneratedNever();
            entity.Property(e => e.Acs_Card)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Door_Pwd)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasDefaultValueSql("((111111))");
            entity.Property(e => e.LastIn_Ctrl)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.LastIn_Time).HasColumnType("datetime");
            entity.Property(e => e.LastOut_Ctrl)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.LastOut_Time).HasColumnType("datetime");
            entity.Property(e => e.Update_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_PersonLastInfo>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__MJ_PersonLastInf__61BB7BD9");

            entity.ToTable("MJ_PersonLastInfo");

            entity.Property(e => e.APB_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.HideBack_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.LastIn_Ctrl)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.LastIn_Time).HasColumnType("datetime");
            entity.Property(e => e.LastOut_Time).HasColumnType("datetime");
        });

        modelBuilder.Entity<MJ_PsnDoorRight>(entity =>
        {
            entity.HasKey(e => new { e.Person_ID, e.Ctrl_No, e.Door_No }).HasName("PK__MJ_PsnDoorRight__147C05D0");

            entity.ToTable("MJ_PsnDoorRight", tb => tb.HasTrigger("MJ_tg_SyncPsnDoorRight"));

            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_PsnInOutTime>(entity =>
        {
            entity.HasKey(e => e.PsnTime_No).HasName("PK__MJ_PsnInOutTime__08162EEB");

            entity.ToTable("MJ_PsnInOutTime");

            entity.Property(e => e.PsnTime_No).ValueGeneratedNever();
            entity.Property(e => e.End_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.PsnTime_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Start_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.holiday)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.restendtime1)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.restendtime2)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.restendtime3)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.restendtime4)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.restendtime5)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.restendtime6)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.restendtime7)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.restendtime8)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.reststarttime1)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.reststarttime2)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.reststarttime3)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.reststarttime4)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.reststarttime5)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.reststarttime6)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.reststarttime7)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.reststarttime8)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.restweek)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.workendtime1)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.workendtime2)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.workendtime3)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.workendtime4)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.workendtime5)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.workendtime6)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.workendtime7)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.workendtime8)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.workstarttime1)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.workstarttime2)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.workstarttime3)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.workstarttime4)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.workstarttime5)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.workstarttime6)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.workstarttime7)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.workstarttime8)
                .HasMaxLength(8)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_PsnInOutTimeSub>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__MJ_PsnInOutTimeS__0AF29B96");

            entity.ToTable("MJ_PsnInOutTimeSub");

            entity.Property(e => e.End_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Group_No)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.Start_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_PsnSpecDate>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__MJ_PsnSpecDate__04459E07");

            entity.ToTable("MJ_PsnSpecDate");

            entity.Property(e => e.Spec_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_PsnSpecTime>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__MJ_PsnSpecTime__062DE679");

            entity.ToTable("MJ_PsnSpecTime");

            entity.Property(e => e.End_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Start_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_ResParam>(entity =>
        {
            entity.HasKey(e => new { e.Ctrl_Type, e.Res_Type }).HasName("PK__MJ_ResParam__52793849");

            entity.ToTable("MJ_ResParam");

            entity.Property(e => e.Ctrl_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Res_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_Solution>(entity =>
        {
            entity.HasKey(e => new { e.Solution_ID, e.Ctrl_No, e.Door_No }).HasName("PK__MJ_Solution__5C02A283");

            entity.ToTable("MJ_Solution");

            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Solution_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Solution_No)
                .HasMaxLength(40)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_SuperRight>(entity =>
        {
            entity.HasKey(e => e.Super_No).HasName("PK__MJ_SuperRight__79C80F94");

            entity.ToTable("MJ_SuperRight");

            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Super_Pwd)
                .HasMaxLength(8)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_SysParam>(entity =>
        {
            entity.HasKey(e => e.Param_ID).HasName("PK__MJ_SysParam__1758727B");

            entity.ToTable("MJ_SysParam");

            entity.Property(e => e.Param_Code)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Param_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Param_Value)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_UrgencyGroup>(entity =>
        {
            entity.HasKey(e => e.UrgGrp_No).HasName("PK__MJ_UrgencyGroup__3F6663D5");

            entity.ToTable("MJ_UrgencyGroup");

            entity.Property(e => e.UrgGrp_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.UrgGrp_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_UrgencyGroupSub>(entity =>
        {
            entity.HasKey(e => new { e.UrgGrp_No, e.Ctrl_No, e.Door_No }).HasName("PK__MJ_UrgencyGroupS__414EAC47");

            entity.ToTable("MJ_UrgencyGroupSub");

            entity.Property(e => e.UrgGrp_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MJ_vi_ChangeHistory>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("MJ_vi_ChangeHistory");

            entity.Property(e => e.Card_NewDate).HasColumnType("datetime");
            entity.Property(e => e.Card_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Card_ValidDate).HasColumnType("datetime");
            entity.Property(e => e.Change_Ctrl)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Change_Date).HasColumnType("datetime");
            entity.Property(e => e.Change_Value)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Door_Pwd)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<SF_MocConsumeMode>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK_ST_MocConsumeSet");

            entity.ToTable("SF_MocConsumeMode");

            entity.Property(e => e.Moc_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Repast_No)
                .HasMaxLength(3)
                .IsUnicode(false);
        });

        modelBuilder.Entity<SF_OrderArea>(entity =>
        {
            entity.HasKey(e => e.Area_No).HasName("PK__SF_OrderArea__7E57BA87");

            entity.ToTable("SF_OrderArea");

            entity.Property(e => e.Area_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Area_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Note)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<SF_OrderDataSub>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__SF_Order__2DF8989E0777106D");

            entity.ToTable("SF_OrderDataSub");

            entity.Property(e => e.Order_No)
                .HasMaxLength(60)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Ware_BeforeFund).HasColumnType("money");
            entity.Property(e => e.Ware_Copies).HasDefaultValue(1);
            entity.Property(e => e.Ware_Fund).HasColumnType("money");
            entity.Property(e => e.Ware_Price).HasColumnType("money");
            entity.Property(e => e.Ware_Taste)
                .HasMaxLength(256)
                .IsUnicode(false);
        });

        modelBuilder.Entity<SF_OrderDatum>(entity =>
        {
            entity.HasKey(e => e.id_Key).HasName("PK__SF_OrderData__041093DD");

            entity.HasIndex(e => new { e.Person_ID, e.Order_date }, "IX_SF_OrderData");

            entity.Property(e => e.Card_No)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Delivery_Addr)
                .HasMaxLength(256)
                .IsUnicode(false);
            entity.Property(e => e.Moc_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Note)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Order_BeforeFund).HasColumnType("money");
            entity.Property(e => e.Order_Fund).HasColumnType("money");
            entity.Property(e => e.Order_No)
                .HasMaxLength(60)
                .IsUnicode(false);
            entity.Property(e => e.Order_Time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Order_date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Repast_No)
                .HasMaxLength(3)
                .IsUnicode(false);
            entity.Property(e => e.Taste)
                .HasMaxLength(256)
                .IsUnicode(false);
            entity.Property(e => e.Telephone)
                .HasMaxLength(36)
                .IsUnicode(false);
            entity.Property(e => e.update_time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
        });

        modelBuilder.Entity<SF_OrderMoc>(entity =>
        {
            entity.HasKey(e => e.Moc_No).HasName("PK__SF_OrderMoc__004002F9");

            entity.ToTable("SF_OrderMoc");

            entity.Property(e => e.Moc_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Area_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Moc_Addr)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.Note)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<SF_OrderRule>(entity =>
        {
            entity.HasKey(e => e.id_Key).HasName("PK__SF_OrderRule__02284B6B");

            entity.ToTable("SF_OrderRule");

            entity.Property(e => e.Moc_No)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Note)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Order_Repast)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Sk_BeginTime)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Sk_EndTime)
                .HasMaxLength(5)
                .IsUnicode(false);
        });

        modelBuilder.Entity<SF_OrderUnUseDatum>(entity =>
        {
            entity.HasKey(e => e.Id_key).HasName("PK__SF_OrderUnUseDat__05F8DC4F");

            entity.Property(e => e.Order_Date)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Order_No)
                .HasMaxLength(60)
                .IsUnicode(false);
            entity.Property(e => e.Repast_No)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<SF_Repast>(entity =>
        {
            entity.HasKey(e => e.Repast_No).HasName("PK__SF_Repast__65C116E7");

            entity.ToTable("SF_Repast");

            entity.Property(e => e.Repast_No)
                .HasMaxLength(3)
                .IsUnicode(false);
            entity.Property(e => e.Bgn_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.End_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Price).HasColumnType("money");
            entity.Property(e => e.Remark)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Repast_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.limit_funds)
                .HasDefaultValue(99999.0m)
                .HasColumnType("money");
            entity.Property(e => e.limit_times).HasDefaultValue(999);
            entity.Property(e => e.second_price).HasColumnType("money");
        });

        modelBuilder.Entity<SK_ScanCode>(entity =>
        {
            entity.HasKey(e => e.ID_Key).HasName("PK__SK_ScanC__2DF994E61AFEE62D");

            entity.ToTable("SK_ScanCode");

            entity.Property(e => e.Get_Time).HasColumnType("datetime");
            entity.Property(e => e.Moc_No)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(32)
                .IsUnicode(false);
            entity.Property(e => e.QR_Code)
                .HasMaxLength(256)
                .IsUnicode(false);
            entity.Property(e => e.QR_Fund).HasColumnType("money");
            entity.Property(e => e.QR_Fund_Free).HasColumnType("money");
            entity.Property(e => e.Remark)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Unit_Price).HasColumnType("money");
            entity.Property(e => e.moc_read_time).HasColumnType("datetime");
            entity.Property(e => e.open_id)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.pay_state).HasDefaultValue(0);
        });

        modelBuilder.Entity<ST_AlterCard>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__ST_AlterCard__0D7A0286");

            entity.ToTable("ST_AlterCard");

            entity.HasIndex(e => e.Person_ID, "XIF1ST_AlterCard");

            entity.Property(e => e.Alter_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Alter_Fee)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.NewCard_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_AlterCardHistory>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__ST_AlterCardHist__38996AB5");

            entity.ToTable("ST_AlterCardHistory");

            entity.HasIndex(e => e.Alter_Date, "IX_ST_AlterCardHistory");

            entity.Property(e => e.Alter_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Alter_Fee)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.NewCard_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_BlackPerson>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__ST_BlackPerson__236943A5");

            entity.ToTable("ST_BlackPerson");

            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Del_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Del_Sign).HasDefaultValue(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_Card>(entity =>
        {
            entity.HasKey(e => e.Person_ID).HasName("PK__ST_Card__3E1D39E1");

            entity.ToTable("ST_Card", tb =>
            {
                tb.HasTrigger("ST_tg_AcsSyncCard");
                tb.HasTrigger("st_tg_card");
            });

            entity.HasIndex(e => e.Type_No, "XIF4ST_Card");

            entity.Property(e => e.Person_ID).ValueGeneratedNever();
            entity.Property(e => e.Card_Balance).HasColumnType("money");
            entity.Property(e => e.Card_Charge).HasColumnType("money");
            entity.Property(e => e.Card_Foregift).HasColumnType("money");
            entity.Property(e => e.Card_NewDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Card_Password)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Card_SmallBalance)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Card_State).HasDefaultValue((byte)0);
            entity.Property(e => e.Card_ValidDate).HasColumnType("datetime");
            entity.Property(e => e.IC_Balance)
                .HasDefaultValue(0m)
                .HasColumnType("money");
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Subsidy_Flag).HasDefaultValue((byte)0);
            entity.Property(e => e.Subsidy_Fund)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Type_No)
                .HasMaxLength(3)
                .IsUnicode(false);
            entity.Property(e => e.Update_Time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_CardInfo>(entity =>
        {
            entity.HasKey(e => e.Card_Type).HasName("PK__ST_CardInfo__498EEC8D");

            entity.ToTable("ST_CardInfo");

            entity.Property(e => e.Card_Type)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Card_Charge).HasColumnType("money");
            entity.Property(e => e.Card_Foregift).HasColumnType("money");
            entity.Property(e => e.Day_DelSysBlack).HasDefaultValue(182);
            entity.Property(e => e.Init_Code)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Passwd_Value)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SmallAccount_Max).HasColumnType("money");
            entity.Property(e => e.Subsidy_CutOffDate).HasColumnType("datetime");
        });

        modelBuilder.Entity<ST_CardType>(entity =>
        {
            entity.HasKey(e => e.Type_No).HasName("PK__ST_CardType__2DE6D218");

            entity.ToTable("ST_CardType");

            entity.Property(e => e.Type_No)
                .HasMaxLength(3)
                .IsUnicode(false);
            entity.Property(e => e.LH_Discount).HasDefaultValue(100);
            entity.Property(e => e.LH_Max_DayFund)
                .HasDefaultValue(99999m)
                .HasColumnType("money");
            entity.Property(e => e.LH_Max_Fund)
                .HasDefaultValue(99999m)
                .HasColumnType("money");
            entity.Property(e => e.Max_IcFunds)
                .HasDefaultValue(100m)
                .HasColumnType("money");
            entity.Property(e => e.Min_Balance).HasColumnType("money");
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SF_Discount).HasDefaultValue(100);
            entity.Property(e => e.SF_Max_Count).HasDefaultValue(99999);
            entity.Property(e => e.SF_Max_DayFund)
                .HasDefaultValue(99999m)
                .HasColumnType("money");
            entity.Property(e => e.SF_Max_Fund)
                .HasDefaultValue(99999m)
                .HasColumnType("money");
            entity.Property(e => e.Small_Discount).HasDefaultValue(100);
            entity.Property(e => e.Subsidy).HasColumnType("money");
            entity.Property(e => e.Type_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Use_MOC)
                .HasMaxLength(3000)
                .IsUnicode(false);
            entity.Property(e => e.Use_Repast)
                .HasMaxLength(3000)
                .IsUnicode(false);
            entity.Property(e => e.Validity).HasDefaultValue(365);
            entity.Property(e => e.type_sign)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_ClearDatum>(entity =>
        {
            entity.HasKey(e => e.Person_ID).HasName("PK__ST_ClearData__7A3223E8");

            entity.Property(e => e.Person_ID).ValueGeneratedNever();
            entity.Property(e => e.Back_Foregift)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Back_Fund)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Card_Charge)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Card_Foregift)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Fund)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Subsidy_Fund)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Supply_Fund)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Take_Fund)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Transfer_Fund)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
        });

        modelBuilder.Entity<ST_ClearTable>(entity =>
        {
            entity.HasKey(e => e.Table_Name).HasName("PK__ST_ClearTable__6D0D32F4");

            entity.ToTable("ST_ClearTable");

            entity.Property(e => e.Table_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Date_Field)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Other_Field)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_DeleteCard>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__ST_DeleteCard__662B2B3B");

            entity.ToTable("ST_DeleteCard");

            entity.HasIndex(e => e.Del_Date, "IX_EM_DelAccount");

            entity.HasIndex(e => e.Person_ID, "XIF1ST_DeleteCard");

            entity.Property(e => e.Back_Foregift)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Back_Fund)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Back_SmallFund)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Card_Charge)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Del_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Fact_Fund).HasColumnType("money");
            entity.Property(e => e.New_Date).HasColumnType("datetime");
            entity.Property(e => e.New_User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_DeleteCardHistory>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__ST_DeleteCardHis__29572725");

            entity.ToTable("ST_DeleteCardHistory");

            entity.HasIndex(e => e.Del_Date, "IX_ST_DeleteCardHistory");

            entity.Property(e => e.Back_Foregift)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Back_Fund)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Back_SmallFund)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Card_Charge)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Del_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Fact_Fund).HasColumnType("money");
            entity.Property(e => e.New_Date).HasColumnType("datetime");
            entity.Property(e => e.New_User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_Department>(entity =>
        {
            entity.HasKey(e => e.Dept_ID).HasName("PK__ST_Department__03F0984C");

            entity.ToTable("ST_Department", tb => tb.HasTrigger("st_tg_ManageDeptRight"));

            entity.Property(e => e.Dept_ID).ValueGeneratedNever();
            entity.Property(e => e.Add_Col1)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Add_Col2)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Create_Time).HasColumnType("datetime");
            entity.Property(e => e.Dept_Chief)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Duty)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Dept_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Telphone)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Is_Del).HasDefaultValue(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_DeptRight>(entity =>
        {
            entity.HasKey(e => new { e.User_No, e.Dept_ID }).HasName("PK__ST_DeptRight__70A8B9AE");

            entity.ToTable("ST_DeptRight");

            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_Dictionary>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__ST_Dictionary__6DCC4D03");

            entity.ToTable("ST_Dictionary");

            entity.Property(e => e.Dict_Id)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Dict_Value)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Is_SysCol).HasDefaultValue(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Table_ChsName)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Table_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_DisableCard>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__ST_DisableCard__72C60C4A");

            entity.ToTable("ST_DisableCard");

            entity.HasIndex(e => e.Disable_Date, "IX_ST_DisableCard");

            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Disable_Cause)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Disable_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_EnCard>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__ST_EnCard__7B5B524B");

            entity.ToTable("ST_EnCard");

            entity.Property(e => e.Card_No)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_Fingerprint>(entity =>
        {
            entity.HasKey(e => e.ID_KEY);

            entity.ToTable("ST_Fingerprint");

            entity.Property(e => e.Person_Name)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Update_Time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
        });

        modelBuilder.Entity<ST_Identity>(entity =>
        {
            entity.HasKey(e => e.Table_Name).HasName("PK__ST_Identity__797309D9");

            entity.ToTable("ST_Identity");

            entity.Property(e => e.Table_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_LockCard>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__ST_LockCard__208CD6FA");

            entity.ToTable("ST_LockCard");

            entity.HasIndex(e => e.Lock_Date, "IX_ST_LockCard");

            entity.HasIndex(e => e.Person_ID, "XIF1ST_LockCard");

            entity.Property(e => e.Lock_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_MOC>(entity =>
        {
            entity.HasKey(e => e.MOC_No).HasName("PK__ST_MOC__45F365D3");

            entity.ToTable("ST_MOC");

            entity.Property(e => e.MOC_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Addr485)
                .HasMaxLength(3)
                .IsUnicode(false);
            entity.Property(e => e.Area_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.COM_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Comm_Content)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.Consume_Mode).HasDefaultValue((byte)1);
            entity.Property(e => e.Download_Status).HasDefaultValue((byte)0);
            entity.Property(e => e.Download_Time).HasColumnType("datetime");
            entity.Property(e => e.IP_Addr)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.Is_Correct).HasDefaultValue(true);
            entity.Property(e => e.Is_Discount).HasDefaultValue(true);
            entity.Property(e => e.Is_OrderCtrl).HasDefaultValue(false);
            entity.Property(e => e.Is_UseSubsidy).HasDefaultValue(true);
            entity.Property(e => e.KQ_MocType).HasDefaultValue((byte)1);
            entity.Property(e => e.KQ_UseType).HasDefaultValue((byte)0);
            entity.Property(e => e.Limit_Fund).HasColumnType("money");
            entity.Property(e => e.MOC_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.MOC_Type)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("MOC");
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.System_No)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Telphone)
                .HasMaxLength(35)
                .IsUnicode(false);
            entity.Property(e => e.Unit_Price).HasColumnType("money");
            entity.Property(e => e.Users_No)
                .HasMaxLength(300)
                .IsUnicode(false);
            entity.Property(e => e.moc_model)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.moc_sn)
                .HasMaxLength(72)
                .IsUnicode(false);
            entity.Property(e => e.name_down_time).HasColumnType("datetime");
        });

        modelBuilder.Entity<ST_MenuInfo>(entity =>
        {
            entity.HasKey(e => e.Menu_ID).HasName("PK__ST_MenuInfo__18EBB532");

            entity.ToTable("ST_MenuInfo", tb => tb.HasTrigger("st_tg_ManageRight"));

            entity.Property(e => e.Menu_ID).ValueGeneratedNever();
            entity.Property(e => e.Call_Mode).HasDefaultValue((byte)1);
            entity.Property(e => e.Form_Name)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Image_Index).HasDefaultValue((short)-1);
            entity.Property(e => e.InToolBar).HasDefaultValue(false);
            entity.Property(e => e.Menu_Hint)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Menu_Index).HasDefaultValue((byte)1);
            entity.Property(e => e.Menu_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Menu_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Para_ID).HasDefaultValue(-1);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Separator).HasDefaultValue((byte)0);
            entity.Property(e => e.ShortKey)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.System_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_NoBalance>(entity =>
        {
            entity.HasKey(e => e.NoBalance_ID).HasName("PK__ST_NoBalance__0697FACD");

            entity.ToTable("ST_NoBalance");

            entity.Property(e => e.NoBalance_ID).ValueGeneratedNever();
            entity.Property(e => e.Adjust_Fund)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Card_Balance)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.DataBase_Balance)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Opt_Project)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Opt_Time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_NoPersonCard>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__ST_NoPersonCard__75A278F5");

            entity.ToTable("ST_NoPersonCard");

            entity.HasIndex(e => e.DownLoad_Date, "IX_ST_NoPersonCard");

            entity.Property(e => e.COM_No)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Card_Balance).HasColumnType("money");
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Card_SmallAccount).HasColumnType("money");
            entity.Property(e => e.Consume_Fund).HasColumnType("money");
            entity.Property(e => e.Consume_Time).HasColumnType("datetime");
            entity.Property(e => e.DB_Balance)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.DownLoad_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.MOC_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Repast_No)
                .HasMaxLength(3)
                .IsUnicode(false);
            entity.Property(e => e.System_No)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_OptLog>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__ST_OptLog__5E8A0973");

            entity.ToTable("ST_OptLog");

            entity.Property(e => e.Opt_Computer)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Opt_Info)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Opt_Table)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Opt_Time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Opt_Type)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.System_No)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.User_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_Person>(entity =>
        {
            entity.HasKey(e => e.Person_ID).HasName("PK__ST_Person__06CD04F7");

            entity.ToTable("ST_Person", tb =>
            {
                tb.HasTrigger("ST_tg_AcsSyncPerson");
                tb.HasTrigger("st_tg_SyncPerson");
                tb.HasTrigger("st_tg_person");
            });

            entity.HasIndex(e => e.Card_No, "IX_ST_Person_Card_No");

            entity.HasIndex(e => e.Person_No, "UQ__ST_Person__72C60C4A");

            entity.HasIndex(e => e.Dept_ID, "XIF1ST_Person");

            entity.Property(e => e.Person_ID).ValueGeneratedNever();
            entity.Property(e => e.Address)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Birthday).HasColumnType("datetime");
            entity.Property(e => e.C_PSD)
                .HasMaxLength(300)
                .IsUnicode(false);
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false)
                .HasDefaultValue("");
            entity.Property(e => e.Card_SN)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Cert_Code)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Cert_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Duty)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Is_Marry).HasDefaultValue(false);
            entity.Property(e => e.Nationality)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.NativePlace)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.OffWork_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.OnWork_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.OpenID)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_Type)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Reserved1)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Reserved2)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Reserved3)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Reserved4)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Reserved5)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Residence)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Sex)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.Title)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UnionID)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.id_number)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.mobile)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.update_time).HasColumnType("datetime");
            entity.Property(e => e.wx_syn_time).HasColumnType("datetime");
        });

        modelBuilder.Entity<ST_PersonAccount>(entity =>
        {
            entity.HasKey(e => e.Person_ID).HasName("PK__ST_PersonAccount__5EBF139D");

            entity.ToTable("ST_PersonAccount");

            entity.Property(e => e.Person_ID).ValueGeneratedNever();
            entity.Property(e => e.Account_Balance).HasColumnType("money");
            entity.Property(e => e.AlterCard_Fund).HasColumnType("money");
            entity.Property(e => e.Back_Foregift).HasColumnType("money");
            entity.Property(e => e.Back_Fund).HasColumnType("money");
            entity.Property(e => e.Back_SmallFund).HasColumnType("money");
            entity.Property(e => e.Card_Charge).HasColumnType("money");
            entity.Property(e => e.Card_Foregift).HasColumnType("money");
            entity.Property(e => e.EM_ConsumeFund).HasColumnType("money");
            entity.Property(e => e.Remark)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Subsidy_Fund).HasColumnType("money");
            entity.Property(e => e.Supply_Fund).HasColumnType("money");
            entity.Property(e => e.Take_Fund).HasColumnType("money");
            entity.Property(e => e.Transfer_Fund).HasColumnType("money");
            entity.Property(e => e.XF_ConsumeFund).HasColumnType("money");
        });

        modelBuilder.Entity<ST_PersonPhoto>(entity =>
        {
            entity.HasKey(e => e.Person_ID).HasName("PK__ST_PersonPhoto__0B91BA14");

            entity.ToTable("ST_PersonPhoto", tb =>
            {
                tb.HasTrigger("st_tg_personphoto");
                tb.HasTrigger("st_tg_personphoto_delete");
            });

            entity.Property(e => e.Person_ID).ValueGeneratedNever();
            entity.Property(e => e.Photo).HasColumnType("image");
            entity.Property(e => e.id_photo).HasColumnType("image");
            entity.Property(e => e.img_qr_code).HasColumnType("image");
            entity.Property(e => e.update_time).HasColumnType("datetime");
        });

        modelBuilder.Entity<ST_RightInfo>(entity =>
        {
            entity.HasKey(e => e.Menu_ID).HasName("PK__ST_RightInfo__55F4C372");

            entity.ToTable("ST_RightInfo");

            entity.Property(e => e.Menu_ID).ValueGeneratedNever();
            entity.Property(e => e.Form_Name)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Right_Values)
                .HasMaxLength(40)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_RightValue>(entity =>
        {
            entity.HasKey(e => new { e.Right_ID, e.Form_Name }).HasName("PK__ST_RightValue__04AFB25B");

            entity.ToTable("ST_RightValue");

            entity.Property(e => e.Right_ID)
                .HasMaxLength(1)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.Form_Name)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Right_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_RoleInfo>(entity =>
        {
            entity.HasKey(e => e.Role_No).HasName("PK__ST_RoleInfo__2739D489");

            entity.ToTable("ST_RoleInfo");

            entity.Property(e => e.Role_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Role_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_RoleRight>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__ST_RoleRight__29221CFB");

            entity.ToTable("ST_RoleRight");

            entity.HasIndex(e => e.Role_No, "XIF1ST_RoleRight");

            entity.HasIndex(e => e.Menu_ID, "XIF2ST_RoleRight");

            entity.Property(e => e.Right_Values)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Role_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_SelfMenu>(entity =>
        {
            entity.HasKey(e => new { e.User_No, e.Menu_ID }).HasName("PK__ST_SelfMenu__0C50D423");

            entity.ToTable("ST_SelfMenu");

            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_SubSysRight>(entity =>
        {
            entity.HasKey(e => new { e.Person_ID, e.System_No }).HasName("PK__ST_SubSysRight__73852659");

            entity.ToTable("ST_SubSysRight", tb => tb.HasTrigger("st_tg_SyncSubSysRight"));

            entity.Property(e => e.System_No)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_SubSystem>(entity =>
        {
            entity.HasKey(e => e.System_No).HasName("PK__ST_SubSystem__7D439ABD");

            entity.ToTable("ST_SubSystem");

            entity.Property(e => e.System_No)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.DBCreateDate).HasColumnType("datetime");
            entity.Property(e => e.DBUpdateinfo)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.DBVersion)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.IsCreateTable).HasDefaultValue(false);
            entity.Property(e => e.IsInitTable).HasDefaultValue(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.System_ExeName)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.System_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.System_Title)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Update_State1).HasDefaultValue((byte)0);
            entity.Property(e => e.Update_State2).HasDefaultValue((byte)0);
            entity.Property(e => e.Update_State3).HasDefaultValue((byte)0);
        });

        modelBuilder.Entity<ST_Subsidy>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__ST_Subsidy__114A936A");

            entity.ToTable("ST_Subsidy");

            entity.HasIndex(e => e.Subsidy_Date, "IX_ST_Subsidys");

            entity.HasIndex(e => e.Person_ID, "XIF1ST_Subsidy");

            entity.Property(e => e.Clear_Consume_BegTime).HasColumnType("datetime");
            entity.Property(e => e.Clear_Consume_EndTime).HasColumnType("datetime");
            entity.Property(e => e.Clear_Subsidy_BegTime).HasColumnType("datetime");
            entity.Property(e => e.Clear_Subsidy_EndTime).HasColumnType("datetime");
            entity.Property(e => e.Create_Time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Plan_Fund)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Subsidy_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Subsidy_Fund).HasColumnType("money");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_SubsidyAccount>(entity =>
        {
            entity.HasKey(e => e.Person_ID).HasName("PK__ST_SubsidyAccoun__6EF57B66");

            entity.ToTable("ST_SubsidyAccount");

            entity.Property(e => e.Person_ID).ValueGeneratedNever();
            entity.Property(e => e.Consume_Fund).HasColumnType("money");
            entity.Property(e => e.Subsidy_Date).HasColumnType("datetime");
            entity.Property(e => e.Subsidy_Fund).HasColumnType("money");
        });

        modelBuilder.Entity<ST_SubsidyHistory>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__ST_SubsidyHistor__412EB0B6");

            entity.ToTable("ST_SubsidyHistory");

            entity.HasIndex(e => e.Subsidy_Date, "IX_ST_SubsidysHistory");

            entity.Property(e => e.Clear_Consume_BegTime).HasColumnType("datetime");
            entity.Property(e => e.Clear_Consume_EndTime).HasColumnType("datetime");
            entity.Property(e => e.Clear_Subsidy_BegTime).HasColumnType("datetime");
            entity.Property(e => e.Clear_Subsidy_EndTime).HasColumnType("datetime");
            entity.Property(e => e.Create_Time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Plan_Fund)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Subsidy_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Subsidy_Fund).HasColumnType("money");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_SupplyFund>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__ST_SupplyFund__756D6ECB");

            entity.ToTable("ST_SupplyFund");

            entity.HasIndex(e => e.Out_Trade_No, "UX_ST_SupplyFund_Out_Trade_No")
                .IsUnique()
                .HasFilter("([Out_Trade_No] IS NOT NULL)");

            entity.HasIndex(e => e.Person_ID, "XIF1ST_SupplyFund");

            entity.Property(e => e.Card_Balance).HasColumnType("money");
            entity.Property(e => e.MOC_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Out_Trade_No)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Supply_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Supply_Fund).HasColumnType("money");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_SupplyFundHistory>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__ST_SupplyFundHis__3C69FB99");

            entity.ToTable("ST_SupplyFundHistory");

            entity.HasIndex(e => e.Supply_Date, "IX_ST_SupplyFundHistory");

            entity.Property(e => e.Card_Balance).HasColumnType("money");
            entity.Property(e => e.MOC_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Out_Trade_No)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Supply_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Supply_Fund).HasColumnType("money");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_SysLog>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__ST_SysLog__57DD0BE4");

            entity.ToTable("ST_SysLog");

            entity.Property(e => e.Opt_Computer)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Opt_Time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Opt_log)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.System_No)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.User_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_SysParam>(entity =>
        {
            entity.HasKey(e => e.Param_ID).HasName("PK__ST_SysParam__540C7B00");

            entity.ToTable("ST_SysParam");

            entity.Property(e => e.Param_Code)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Param_Name)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Param_Value)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_SystemBalance>(entity =>
        {
            entity.HasKey(e => e.Balance_Time).HasName("PK__ST_SystemBalance__5165187F");

            entity.ToTable("ST_SystemBalance");

            entity.Property(e => e.Balance_Time).HasColumnType("datetime");
            entity.Property(e => e.Back_Foregift).HasColumnType("money");
            entity.Property(e => e.Back_Fund).HasColumnType("money");
            entity.Property(e => e.Card_AllBalance).HasColumnType("money");
            entity.Property(e => e.Card_Charge).HasColumnType("money");
            entity.Property(e => e.Card_Foregift).HasColumnType("money");
            entity.Property(e => e.Consume_AllFund).HasColumnType("money");
            entity.Property(e => e.Remain_AllForegift).HasColumnType("money");
            entity.Property(e => e.Remark)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Subsidy_AllFund).HasColumnType("money");
            entity.Property(e => e.Supply_AllFund).HasColumnType("money");
            entity.Property(e => e.System_Balance).HasColumnType("money");
            entity.Property(e => e.Take_AllFund).HasColumnType("money");
            entity.Property(e => e.Transfer_AllFund).HasColumnType("money");
        });

        modelBuilder.Entity<ST_TakeFund>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__ST_TakeFund__503BEA1C");

            entity.ToTable("ST_TakeFund");

            entity.HasIndex(e => e.Take_Date, "IX_ST_TakeFund");

            entity.HasIndex(e => e.Person_ID, "XIF1ST_TakeFund");

            entity.Property(e => e.Card_Balance).HasColumnType("money");
            entity.Property(e => e.MOC_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Take_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Take_Fund).HasColumnType("money");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_TakeFundHistory>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__ST_TakeFundHisto__34C8D9D1");

            entity.ToTable("ST_TakeFundHistory");

            entity.HasIndex(e => e.Take_Date, "IX_ST_TakeFundHistory");

            entity.Property(e => e.Card_Balance).HasColumnType("money");
            entity.Property(e => e.MOC_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Take_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Take_Fund).HasColumnType("money");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_TrafficLog2201>(entity =>
        {
            entity.HasKey(e => e.ID_Key).HasName("PK__ST_Traff__2DF994E6375B2DB9");

            entity.ToTable("ST_TrafficLog2201");

            entity.Property(e => e.Async_Date)
                .HasMaxLength(19)
                .IsUnicode(false);
            entity.Property(e => e.Create_Date).HasColumnType("datetime");
            entity.Property(e => e.Ctrl_Name)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Door_Name)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.EventType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Photo).IsUnicode(false);
            entity.Property(e => e.PhotoPath)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(1000)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_TransferAccount>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__ST_TransferAccou__5AB9788F");

            entity.ToTable("ST_TransferAccount");

            entity.HasIndex(e => e.Transfer_Date, "IX_ST_TransferAccount");

            entity.HasIndex(e => e.Person_ID, "XIF1ST_TransferAccount");

            entity.Property(e => e.Card_Balance).HasColumnType("money");
            entity.Property(e => e.MOC_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Transfer_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Transfer_Fund).HasColumnType("money");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_TransferAccountHistory>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__ST_TransferAccou__30F848ED");

            entity.ToTable("ST_TransferAccountHistory");

            entity.HasIndex(e => e.Transfer_Date, "IX_ST_TransferAccountHistory");

            entity.Property(e => e.Card_Balance).HasColumnType("money");
            entity.Property(e => e.MOC_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Transfer_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Transfer_Fund).HasColumnType("money");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_UnLockCard>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__ST_UnLockCard__2B0A656D");

            entity.ToTable("ST_UnLockCard");

            entity.HasIndex(e => e.UnLock_Date, "IX_ST_UnLockCard");

            entity.HasIndex(e => e.Person_ID, "XIF1ST_UnLockCard");

            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UnLock_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ST_UserInfo>(entity =>
        {
            entity.HasKey(e => e.User_No).HasName("PK__ST_UserInfo__6166761E");

            entity.ToTable("ST_UserInfo");

            entity.HasIndex(e => e.Role_No, "XIF1ST_UserInfo");

            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Computer_Name)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Current_Menu)
                .HasMaxLength(3)
                .IsUnicode(false)
                .HasDefaultValue("ST");
            entity.Property(e => e.Is_Del).HasDefaultValue(false);
            entity.Property(e => e.Login_Time).HasColumnType("datetime");
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Role_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.User_Moc)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.User_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.User_Pwd)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.User_Type).HasDefaultValue((byte)0);
            entity.Property(e => e.Using_Form)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Using_Model)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_KQAdjustClass>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_KQAdjustClass");

            entity.Property(e => e.Adjust_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Class_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Class_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Dept_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date).HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_KQAdjustClassHistory>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_KQAdjustClassHistory");

            entity.Property(e => e.Adjust_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Class_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Class_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Dept_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date).HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_KQAdjustRest>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_KQAdjustRest");

            entity.Property(e => e.Adjust_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Dept_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date).HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_KQAdjustRestHistory>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_KQAdjustRestHistory");

            entity.Property(e => e.Adjust_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Dept_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date).HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_KQAttendInfo>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_KQAttendInfo");

            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Class_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Class_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Dept_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Rest_Days)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date).HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_KQBrushCard>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_KQBrushCard");

            entity.Property(e => e.Brush_Data)
                .HasMaxLength(256)
                .IsUnicode(false);
            entity.Property(e => e.Brush_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.dept_name)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.dept_no)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.person_name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.person_no)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_KQBrushCardHistory>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_KQBrushCardHistory");

            entity.Property(e => e.Brush_Data)
                .HasMaxLength(256)
                .IsUnicode(false);
            entity.Property(e => e.Brush_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.dept_name)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.dept_no)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.person_name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.person_no)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_KQClassInfo>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_KQClassInfo");

            entity.Property(e => e.Class_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Class_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Class_Type)
                .HasMaxLength(1)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_KQFillAttend>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_KQFillAttend");

            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Dept_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Fill_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date).HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_KQFillAttendHistory>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_KQFillAttendHistory");

            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Dept_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Fill_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date).HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_KQLeave>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_KQLeave");

            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Dept_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Kind_Name)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Kind_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Leave_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Leave_TimeBgn)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Leave_TimeEnd)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date).HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_KQLeaveHistory>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_KQLeaveHistory");

            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Dept_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Kind_Name)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Kind_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Leave_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Leave_TimeBgn)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Leave_TimeEnd)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date).HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_KQOvertime>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_KQOvertime");

            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Dept_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Over_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date).HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_KQOvertimeHistory>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_KQOvertimeHistory");

            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Dept_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Over_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date).HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_KQPerson>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_KQPerson");

            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Class_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Dept_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Rest_Days)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date).HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_KQSupplyTime>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_KQSupplyTime");

            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Dept_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Supply_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Supply_Reason)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Supply_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date).HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_KQSupplyTimeHistory>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_KQSupplyTimeHistory");

            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Dept_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Supply_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Supply_Reason)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Supply_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Update_Date).HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_MJAbnormRecord>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_MJAbnormRecord");

            entity.Property(e => e.BrushCard_Time).HasColumnType("datetime");
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Ctrl_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Dept_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Door_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Event_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_MJAlarmRecord>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_MJAlarmRecord");

            entity.Property(e => e.Alarm_Time).HasColumnType("datetime");
            entity.Property(e => e.Ctrl_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Dot_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Event_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_MJDotLinkage>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_MJDotLinkage");

            entity.Property(e => e.Ctrl_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Ctrl_Type)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Dot_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Dot_No)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Linkage_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Linkage_No)
                .HasMaxLength(4)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_MJMonitorRecord>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_MJMonitorRecord");

            entity.Property(e => e.Alarm_Info)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.Card_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Ctrl_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Dept_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Description)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Door_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Run_Info)
                .HasMaxLength(80)
                .IsUnicode(false);
            entity.Property(e => e.Run_Time).HasColumnType("datetime");
            entity.Property(e => e.State_Info)
                .HasMaxLength(40)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_MJNormalRecord>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_MJNormalRecord");

            entity.Property(e => e.BrushCard_Time).HasColumnType("datetime");
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Ctrl_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Dept_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Door_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Event_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_MJPersonInfo>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_MJPersonInfo");

            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Dept_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Door_Pwd)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.OnWork_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_MJPersonIntOutTime>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_MJPersonIntOutTime");

            entity.Property(e => e.Ctrl_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Ctrl_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Door_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.PsnTime_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_STAlterCard>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_STAlterCard");

            entity.Property(e => e.Alter_Date).HasColumnType("datetime");
            entity.Property(e => e.Alter_Fee).HasColumnType("money");
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Dept_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.NewCard_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_STPerson>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_STPerson");

            entity.Property(e => e.address)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.birthday).HasColumnType("datetime");
            entity.Property(e => e.card_no)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.cert_code)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.cert_name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.dept_name)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.dept_no)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.duty)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.is_marry)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.nationality)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.nativePlace)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.person_name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.person_no)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.residence)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.sex)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.title)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_STPersonCard>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_STPersonCard");

            entity.Property(e => e.Card_Balance).HasColumnType("money");
            entity.Property(e => e.Card_NewDate).HasColumnType("datetime");
            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Card_Password)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Card_SmallBalance).HasColumnType("money");
            entity.Property(e => e.Card_ValidDate).HasColumnType("datetime");
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Dept_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Max_IcFunds).HasColumnType("money");
            entity.Property(e => e.OffWork_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Subsidy_Fund).HasColumnType("money");
            entity.Property(e => e.Type_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Type_No)
                .HasMaxLength(3)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_STSubSidy>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_STSubSidy");

            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Dept_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Plan_Fund).HasColumnType("money");
            entity.Property(e => e.Subsidy_Date).HasColumnType("datetime");
            entity.Property(e => e.Subsidy_Fund).HasColumnType("money");
            entity.Property(e => e.Type_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Type_No)
                .HasMaxLength(3)
                .IsUnicode(false);
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_XFConsumeCleanup>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_XFConsumeCleanup");

            entity.Property(e => e.COM_No)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Card_Balance).HasColumnType("money");
            entity.Property(e => e.Card_SmallAccount).HasColumnType("money");
            entity.Property(e => e.Consume_Fund).HasColumnType("money");
            entity.Property(e => e.Consume_Time).HasColumnType("datetime");
            entity.Property(e => e.DB_Balance).HasColumnType("money");
            entity.Property(e => e.Dept_NO)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.MOC_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.MOC_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Person_NO)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Repast_No)
                .HasMaxLength(3)
                .IsUnicode(false);
            entity.Property(e => e.System_No)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_XFConsumeFake>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_XFConsumeFake");

            entity.Property(e => e.COM_No)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Card_Balance).HasColumnType("money");
            entity.Property(e => e.Card_SmallAccount).HasColumnType("money");
            entity.Property(e => e.Consume_Fund).HasColumnType("money");
            entity.Property(e => e.Consume_Time).HasColumnType("datetime");
            entity.Property(e => e.DB_Balance).HasColumnType("money");
            entity.Property(e => e.Dept_NO)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.MOC_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.MOC_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Person_NO)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(1)
                .IsUnicode(false);
            entity.Property(e => e.Repast_No)
                .HasMaxLength(3)
                .IsUnicode(false);
            entity.Property(e => e.System_No)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_XFPsnConsume>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_XFPsnConsume");

            entity.Property(e => e.COM_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Card_Balance).HasColumnType("money");
            entity.Property(e => e.Card_SmallAccount).HasColumnType("money");
            entity.Property(e => e.Consume_Fund).HasColumnType("money");
            entity.Property(e => e.Consume_Time).HasColumnType("datetime");
            entity.Property(e => e.DB_Balance).HasColumnType("money");
            entity.Property(e => e.Dept_NO)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.MOC_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.MOC_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Person_NO)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Repast_No)
                .HasMaxLength(3)
                .IsUnicode(false);
            entity.Property(e => e.System_No)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_XFPsnConsume_Ex>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_XFPsnConsume_Ex");

            entity.Property(e => e.COM_No)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Card_Balance).HasColumnType("money");
            entity.Property(e => e.Card_SmallAccount).HasColumnType("money");
            entity.Property(e => e.Consume_Fund).HasColumnType("money");
            entity.Property(e => e.Consume_Time).HasColumnType("datetime");
            entity.Property(e => e.DB_Balance).HasColumnType("money");
            entity.Property(e => e.Dept_NO)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Dept_Name)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.MOC_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.MOC_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Person_NO)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Repast_No)
                .HasMaxLength(3)
                .IsUnicode(false);
            entity.Property(e => e.System_No)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<V_XFSectArea>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_XFSectArea");

            entity.Property(e => e.Area_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Sect_Name)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Sect_No)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.Term_No)
                .HasMaxLength(4)
                .IsUnicode(false);
        });

        modelBuilder.Entity<VisitorApply>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__VisitorA__2DF8989E5E3FF0B0");

            entity.ToTable("VisitorApply", tb => tb.HasTrigger("VisitorApplyWXUpdate"));

            entity.Property(e => e.Apply_No)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Approved_Reason)
                .HasMaxLength(300)
                .IsUnicode(false);
            entity.Property(e => e.Approved_Time).HasColumnType("datetime");
            entity.Property(e => e.Approved_User)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Carry_items)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.CreateTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Empower_Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.End_Date).HasColumnType("datetime");
            entity.Property(e => e.Is_Up).HasDefaultValue(false);
            entity.Property(e => e.OpenID)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Person_Mobile)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Start_Date).HasColumnType("datetime");
            entity.Property(e => e.Subject_Matter)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.VisitorID_number)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Visitor_Company)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.Visitor_Number)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.car_number)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.check_intime).HasColumnType("datetime");
            entity.Property(e => e.check_outtime).HasColumnType("datetime");
            entity.Property(e => e.syn_time).HasColumnType("datetime");
            entity.Property(e => e.update_time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.visitor_type)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.visitors_number).HasDefaultValue(1);
        });

        modelBuilder.Entity<VisitorEntourage>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__VisitorE__2DF8989E65E11278");

            entity.ToTable("VisitorEntourage");

            entity.Property(e => e.CreateTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Entourage1_IDnumber)
                .HasMaxLength(18)
                .IsUnicode(false);
            entity.Property(e => e.Entourage1_Name)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Entourage1_Number)
                .HasMaxLength(11)
                .IsUnicode(false);
            entity.Property(e => e.Entourage2_IDnumber)
                .HasMaxLength(18)
                .IsUnicode(false);
            entity.Property(e => e.Entourage2_Name)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Entourage2_Number)
                .HasMaxLength(11)
                .IsUnicode(false);
            entity.Property(e => e.Entourage3_IDnumber)
                .HasMaxLength(18)
                .IsUnicode(false);
            entity.Property(e => e.Entourage3_Name)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Entourage3_Number)
                .HasMaxLength(11)
                .IsUnicode(false);
            entity.Property(e => e.Entourage4_IDnumber)
                .HasMaxLength(18)
                .IsUnicode(false);
            entity.Property(e => e.Entourage4_Name)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Entourage4_Number)
                .HasMaxLength(11)
                .IsUnicode(false);
            entity.Property(e => e.Entourage5_IDnumber)
                .HasMaxLength(18)
                .IsUnicode(false);
            entity.Property(e => e.Entourage5_Name)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Entourage5_Number)
                .HasMaxLength(11)
                .IsUnicode(false);
            entity.Property(e => e.Visitor_Number)
                .HasMaxLength(15)
                .IsUnicode(false);
        });

        modelBuilder.Entity<VisitorUser>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__VisitorU__2DF8989E5792F321");

            entity.ToTable("VisitorUser", tb => tb.HasTrigger("VisitorUserWXUpdate"));

            entity.Property(e => e.CreateTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ID_Type)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Is_Up).HasDefaultValue(false);
            entity.Property(e => e.OpenID)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.VisitorID_number)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.VisitorPhoto).HasColumnType("image");
            entity.Property(e => e.VisitorPhotoUrl)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.Visitor_Company)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.Visitor_Name)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.syn_time).HasColumnType("datetime");
            entity.Property(e => e.update_time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
        });

        modelBuilder.Entity<WX_ApiLog>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__WX_ApiLo__2DF8989E770B9E7A");

            entity.ToTable("WX_ApiLog");

            entity.Property(e => e.Api_Url)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.Create_Time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Http_Method)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.IP)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<WX_Feedback>(entity =>
        {
            entity.HasKey(e => e.ID_KEY);

            entity.ToTable("WX_Feedback");

            entity.HasIndex(e => new { e.Person_ID, e.Create_Time }, "IX_WX_Feedback_Person").IsDescending(false, true);

            entity.HasIndex(e => new { e.Status, e.Create_Time }, "IX_WX_Feedback_Status").IsDescending(false, true);

            entity.HasIndex(e => e.Ticket_No, "UX_WX_Feedback_TicketNo").IsUnique();

            entity.Property(e => e.Card_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Contact).HasMaxLength(100);
            entity.Property(e => e.Content).HasMaxLength(1000);
            entity.Property(e => e.Create_Time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Feed_Type).HasMaxLength(20);
            entity.Property(e => e.Person_Name).HasMaxLength(50);
            entity.Property(e => e.Person_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Reply_Content).HasMaxLength(1000);
            entity.Property(e => e.Reply_Time).HasColumnType("datetime");
            entity.Property(e => e.Reply_User).HasMaxLength(50);
            entity.Property(e => e.Source)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Ticket_No)
                .HasMaxLength(30)
                .IsUnicode(false);
        });

        modelBuilder.Entity<WX_FeedbackImage>(entity =>
        {
            entity.HasKey(e => e.ID_KEY);

            entity.ToTable("WX_FeedbackImage");

            entity.HasIndex(e => new { e.Ticket_No, e.Seq }, "IX_WX_FeedbackImage_Ticket");

            entity.Property(e => e.Content_Type)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Create_Time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Ticket_No)
                .HasMaxLength(30)
                .IsUnicode(false);
        });

        modelBuilder.Entity<WX_KQ_Appeal>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__WX_KQ_Ap__2DF8989E6A5C7A29");

            entity.ToTable("WX_KQ_Appeal");

            entity.Property(e => e.Appeal_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Appeal_Type)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Create_Time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Reason)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("pending");
            entity.Property(e => e.Update_Time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.User_No)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<WX_KQ_Apply>(entity =>
        {
            entity.HasKey(e => e.ID_KEY);

            entity.ToTable("WX_KQ_Apply");

            entity.HasIndex(e => new { e.Status, e.Apply_Type, e.Create_Time }, "IX_WX_KQ_Apply_Audit").IsDescending(false, false, true);

            entity.HasIndex(e => new { e.Person_ID, e.Create_Time }, "IX_WX_KQ_Apply_Person").IsDescending(false, true);

            entity.HasIndex(e => e.Apply_No, "UX_WX_KQ_Apply_No").IsUnique();

            entity.Property(e => e.Appeal_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Appeal_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Appeal_Type).HasMaxLength(20);
            entity.Property(e => e.Apply_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Apply_Type)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Audit_Remark).HasMaxLength(200);
            entity.Property(e => e.Audit_Time).HasColumnType("datetime");
            entity.Property(e => e.Audit_User).HasMaxLength(50);
            entity.Property(e => e.Create_Time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Kind_Name).HasMaxLength(20);
            entity.Property(e => e.Kind_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Leave_Bgn)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Leave_End)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Person_Name).HasMaxLength(50);
            entity.Property(e => e.Person_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Reason).HasMaxLength(200);
            entity.Property(e => e.Source)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Supply_Date)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Supply_Time)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.Target_Ids).HasMaxLength(400);
            entity.Property(e => e.Target_Table)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<WX_Message>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__WX_Messa__2DF8989E08362A7C");

            entity.ToTable("WX_Message");

            entity.Property(e => e.Content).HasMaxLength(1000);
            entity.Property(e => e.Create_Time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Is_Read).HasDefaultValue(false);
            entity.Property(e => e.Msg_Type)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Title).HasMaxLength(200);
        });

        modelBuilder.Entity<WX_RechargeLog>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__WX_Recha__2DF8989E7246E95D");

            entity.ToTable("WX_RechargeLog");

            entity.HasIndex(e => e.Out_Trade_No, "IX_WX_RechargeLog_Out_Trade_No");

            entity.Property(e => e.Channel)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Create_Time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Err_Code)
                .HasMaxLength(32)
                .IsUnicode(false);
            entity.Property(e => e.Err_Msg)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Out_Trade_No)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Total_Fee).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Trade_Type)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Transaction_Id)
                .HasMaxLength(64)
                .IsUnicode(false);
            entity.Property(e => e.Update_Time).HasColumnType("datetime");
        });

        modelBuilder.Entity<WX_SmsCode>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__WX_SmsCo__2DF8989E7BD05397");

            entity.ToTable("WX_SmsCode");

            entity.Property(e => e.Code)
                .HasMaxLength(8)
                .IsUnicode(false);
            entity.Property(e => e.Create_Time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Expire_Time).HasColumnType("datetime");
            entity.Property(e => e.Mobile)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Scene)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Used).HasDefaultValue(false);
        });

        modelBuilder.Entity<WX_VisitorCard>(entity =>
        {
            entity.HasKey(e => e.Card_No);

            entity.ToTable("WX_VisitorCard");

            entity.HasIndex(e => new { e.State, e.Card_No }, "IX_WX_VisitorCard_State");

            entity.Property(e => e.Card_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Apply_No)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Bind_Time).HasColumnType("datetime");
            entity.Property(e => e.Release_Time).HasColumnType("datetime");
            entity.Property(e => e.Remark).HasMaxLength(100);
        });

        modelBuilder.Entity<XF_Area>(entity =>
        {
            entity.HasKey(e => e.Area_No).HasName("PK__XF_Area__595B4002");

            entity.ToTable("XF_Area");

            entity.Property(e => e.Area_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Area_Name)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Sect_No)
                .HasMaxLength(4)
                .IsUnicode(false);

            entity.HasOne(d => d.Sect_NoNavigation).WithMany(p => p.XF_Areas)
                .HasForeignKey(d => d.Sect_No)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__XF_Area__Sect_No__5A4F643B");
        });

        modelBuilder.Entity<XF_Collect>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__XF_Collect__558AAF1E");

            entity.ToTable("XF_Collect");

            entity.Property(e => e.IP_Addr)
                .HasMaxLength(15)
                .IsUnicode(false);
        });

        modelBuilder.Entity<XF_CollectLog>(entity =>
        {
            entity.HasKey(e => e.Id_Key).HasName("PK__XF_CollectLog__63D8CE75");

            entity.ToTable("XF_CollectLog");

            entity.Property(e => e.Log_DateTime).HasColumnType("datetime");
            entity.Property(e => e.Log_Msg)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<XF_Consume>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__XF_Consume__09DE7BCC");

            entity.ToTable("XF_Consume");

            entity.HasIndex(e => new { e.Person_ID, e.Consume_Time }, "Consume_Person_Idx");

            entity.HasIndex(e => e.Consume_Time, "IX_XF_Consume");

            entity.HasIndex(e => e.Person_ID, "IX_XF_Consume_Person_ID");

            entity.Property(e => e.BarCode)
                .HasMaxLength(800)
                .IsUnicode(false);
            entity.Property(e => e.Before_Fund).HasColumnType("money");
            entity.Property(e => e.COM_No)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Card_Balance).HasColumnType("money");
            entity.Property(e => e.Card_SmallAccount)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Consume_Capacity).HasDefaultValue(0.0);
            entity.Property(e => e.Consume_Copy).HasDefaultValue(0);
            entity.Property(e => e.Consume_Fund).HasColumnType("money");
            entity.Property(e => e.Consume_Time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Consume_TimeLen).HasDefaultValue(0.0);
            entity.Property(e => e.DB_Balance)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Download_Time).HasColumnType("datetime");
            entity.Property(e => e.MOC_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.OrderID)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Repast_No)
                .HasMaxLength(3)
                .IsUnicode(false);
            entity.Property(e => e.System_No)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("SF");
            entity.Property(e => e.wx_syn_time).HasColumnType("datetime");
        });

        modelBuilder.Entity<XF_ConsumeCleanup>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__XF_ConsumeCleanu__61F08603");

            entity.ToTable("XF_ConsumeCleanup");

            entity.Property(e => e.Before_Fund).HasColumnType("money");
            entity.Property(e => e.COM_No)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Card_Balance).HasColumnType("money");
            entity.Property(e => e.Card_SmallAccount).HasColumnType("money");
            entity.Property(e => e.Consume_Fund).HasColumnType("money");
            entity.Property(e => e.Consume_Time).HasColumnType("datetime");
            entity.Property(e => e.DB_Balance).HasColumnType("money");
            entity.Property(e => e.Download_Time).HasColumnType("datetime");
            entity.Property(e => e.MOC_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Repast_No)
                .HasMaxLength(3)
                .IsUnicode(false);
            entity.Property(e => e.SalesPerson)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.System_No)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<XF_ConsumeDetail>(entity =>
        {
            entity.HasKey(e => e.id_key).HasName("PK__XF_Consu__D496202D1E5A75C5");

            entity.ToTable("XF_ConsumeDetail");

            entity.HasIndex(e => e.order_no, "IX_XF_ConsumeDetail_order_no");

            entity.Property(e => e.consume_time).HasColumnType("datetime");
            entity.Property(e => e.moc_no)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.order_no)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.ware_before_price).HasColumnType("money");
            entity.Property(e => e.ware_funds).HasColumnType("money");
            entity.Property(e => e.ware_price).HasColumnType("money");
        });

        modelBuilder.Entity<XF_ConsumeDetailHistory>(entity =>
        {
            entity.HasKey(e => e.id_key).HasName("PK__XF_Consu__D496202D222B06A9");

            entity.ToTable("XF_ConsumeDetailHistory");

            entity.Property(e => e.consume_time).HasColumnType("datetime");
            entity.Property(e => e.moc_no)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.order_no)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.ware_before_price).HasColumnType("money");
            entity.Property(e => e.ware_funds).HasColumnType("money");
            entity.Property(e => e.ware_price).HasColumnType("money");
        });

        modelBuilder.Entity<XF_ConsumeExcept>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__XF_Consu__2DF8989E0A888742");

            entity.ToTable("XF_ConsumeExcept");

            entity.Property(e => e.Card_Balance).HasColumnType("money");
            entity.Property(e => e.Card_no)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Consume_Fund).HasColumnType("money");
            entity.Property(e => e.Consume_Time)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Download_Time).HasColumnType("datetime");
            entity.Property(e => e.MOC_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Ware_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.info1)
                .HasMaxLength(1000)
                .IsUnicode(false);
            entity.Property(e => e.info2)
                .HasMaxLength(1000)
                .IsUnicode(false);
            entity.Property(e => e.info3)
                .HasMaxLength(1000)
                .IsUnicode(false);
        });

        modelBuilder.Entity<XF_ConsumeFake>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__XF_ConsumeFake__21B6055D");

            entity.ToTable("XF_ConsumeFake");

            entity.Property(e => e.Before_Fund)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.COM_No)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Card_Balance).HasColumnType("money");
            entity.Property(e => e.Card_SmallAccount)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Consume_Copy).HasDefaultValue(0);
            entity.Property(e => e.Consume_Fund).HasColumnType("money");
            entity.Property(e => e.Consume_Time).HasColumnType("datetime");
            entity.Property(e => e.DB_Balance)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Download_Time).HasColumnType("datetime");
            entity.Property(e => e.MOC_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Matching_Time).HasColumnType("datetime");
            entity.Property(e => e.Repast_No)
                .HasMaxLength(3)
                .IsUnicode(false);
            entity.Property(e => e.SalesPerson)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.System_No)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<XF_ConsumeHistory>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__XF_ConsumeHistor__164452B1");

            entity.ToTable("XF_ConsumeHistory");

            entity.HasIndex(e => e.Consume_Time, "IX_XF_ConsumeHistory");

            entity.HasIndex(e => e.Person_ID, "IX_XF_ConsumeHistory_Person_ID");

            entity.Property(e => e.BarCode)
                .HasMaxLength(800)
                .IsUnicode(false);
            entity.Property(e => e.COM_No)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Card_Balance).HasColumnType("money");
            entity.Property(e => e.Card_SmallAccount)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Consume_Capacity).HasDefaultValue(0.0);
            entity.Property(e => e.Consume_Copy).HasDefaultValue(0);
            entity.Property(e => e.Consume_Fund).HasColumnType("money");
            entity.Property(e => e.Consume_Time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Consume_TimeLen).HasDefaultValue(0.0);
            entity.Property(e => e.DB_Balance)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.MOC_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.OrderID)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Repast_No)
                .HasMaxLength(3)
                .IsUnicode(false);
            entity.Property(e => e.System_No)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("SF");
        });

        modelBuilder.Entity<XF_ConsumeTemp>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__XF_ConsumeTemp__7C8480AE");

            entity.ToTable("XF_ConsumeTemp");

            entity.HasIndex(e => new { e.Person_ID, e.Consume_Time, e.System_No }, "ConsumeTemp_Person_Idx");

            entity.HasIndex(e => e.Consume_Time, "IX_XF_ConsumeTemp");

            entity.Property(e => e.BarCode)
                .HasMaxLength(800)
                .IsUnicode(false);
            entity.Property(e => e.Before_Fund).HasColumnType("money");
            entity.Property(e => e.COM_No)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Card_Balance).HasColumnType("money");
            entity.Property(e => e.Card_SmallAccount)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Consume_Capacity).HasDefaultValue(0.0);
            entity.Property(e => e.Consume_Copy).HasDefaultValue(0);
            entity.Property(e => e.Consume_Fund).HasColumnType("money");
            entity.Property(e => e.Consume_Time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Consume_TimeLen).HasDefaultValue(0.0);
            entity.Property(e => e.DB_Balance)
                .HasDefaultValue(0.00m)
                .HasColumnType("money");
            entity.Property(e => e.Download_Time).HasColumnType("datetime");
            entity.Property(e => e.Is_Commited).HasDefaultValue(false);
            entity.Property(e => e.MOC_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.OrderID)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Repast_No)
                .HasMaxLength(3)
                .IsUnicode(false);
            entity.Property(e => e.System_No)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("SF");
        });

        modelBuilder.Entity<XF_KeyType>(entity =>
        {
            entity.HasKey(e => new { e.Key_Type, e.Key_Value }).HasName("PK__XF_KeyType__5C37ACAD");

            entity.ToTable("XF_KeyType");

            entity.Property(e => e.Key_Fund).HasColumnType("money");
            entity.Property(e => e.Key_TypeName)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<XF_OrderPending>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__XF_Order__2DF8989E131DCD43");

            entity.ToTable("XF_OrderPending");

            entity.Property(e => e.Moc_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Order_No)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Pending_Time).HasColumnType("datetime");
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Ware_All_Funds).HasColumnType("money");
        });

        modelBuilder.Entity<XF_OrderPendingSub>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__XF_Order__2DF8989E16EE5E27");

            entity.ToTable("XF_OrderPendingSub");

            entity.Property(e => e.Order_No)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Ware_Funds).HasColumnType("money");
            entity.Property(e => e.Ware_Price).HasColumnType("money");
        });

        modelBuilder.Entity<XF_PayRecord>(entity =>
        {
            entity.HasKey(e => e.ID_Key).HasName("PK__XF_PayRe__2DF994E66BCEF5F8");

            entity.ToTable("XF_PayRecord");

            entity.Property(e => e.BarCode)
                .HasMaxLength(800)
                .IsUnicode(false);
            entity.Property(e => e.Before_Fund).HasColumnType("money");
            entity.Property(e => e.Card_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Consume_Fund).HasColumnType("money");
            entity.Property(e => e.Consume_Time).HasColumnType("datetime");
            entity.Property(e => e.Moc_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.OrderID)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Repast_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Subsidy_Fund).HasColumnType("money");
            entity.Property(e => e.System_No)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Ware_ID)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.pay_type)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.recv_info)
                .HasMaxLength(1000)
                .IsUnicode(false);
        });

        modelBuilder.Entity<XF_PayRecordTemp>(entity =>
        {
            entity.HasKey(e => e.ID_Key).HasName("PK__XF_PayRe__2DF994E66F9F86DC");

            entity.ToTable("XF_PayRecordTemp");

            entity.Property(e => e.BarCode)
                .HasMaxLength(800)
                .IsUnicode(false);
            entity.Property(e => e.Before_Fund).HasColumnType("money");
            entity.Property(e => e.Card_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Consume_Fund).HasColumnType("money");
            entity.Property(e => e.Consume_Time).HasColumnType("datetime");
            entity.Property(e => e.Moc_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.OrderID)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Repast_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Subsidy_Fund).HasColumnType("money");
            entity.Property(e => e.System_No)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Ware_ID)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.pay_type)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.recv_info)
                .HasMaxLength(1000)
                .IsUnicode(false);
        });

        modelBuilder.Entity<XF_Section>(entity =>
        {
            entity.HasKey(e => e.Sect_No).HasName("PK__XF_Section__5772F790");

            entity.ToTable("XF_Section");

            entity.Property(e => e.Sect_No)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Sect_Name)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<XF_Subsidy>(entity =>
        {
            entity.HasKey(e => e.ID_Key).HasName("PK__XF_Subsidy__160F4887");

            entity.ToTable("XF_Subsidy");

            entity.HasIndex(e => new { e.Card_No, e.Apply_Time }, "IX_XF_Subsidy");

            entity.Property(e => e.Apply_Time).HasColumnType("datetime");
            entity.Property(e => e.Card_Balance)
                .HasDefaultValue(0m)
                .HasColumnType("money");
            entity.Property(e => e.Card_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Moc_No)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Subsidy_Fund).HasColumnType("money");
        });

        modelBuilder.Entity<XF_SysParam>(entity =>
        {
            entity.HasKey(e => e.Param_ID).HasName("PK__XF_SysParam__53A266AC");

            entity.ToTable("XF_SysParam");

            entity.Property(e => e.Param_Code)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Param_Name)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Param_Value)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<XF_WareInWeek>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__XF_WareI__2DF8989E2BE97B0D");

            entity.ToTable("XF_WareInWeek");

            entity.Property(e => e.Area_No)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(300)
                .IsUnicode(false);
            entity.Property(e => e.Repast_No)
                .HasMaxLength(3)
                .IsUnicode(false);
            entity.Property(e => e.Ware_Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
        });

        modelBuilder.Entity<XF_WareInfo>(entity =>
        {
            entity.HasKey(e => new { e.Ware_ID, e.System_No }).HasName("PK__XF_WareInfo__5F141958");

            entity.ToTable("XF_WareInfo");

            entity.Property(e => e.System_No)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Ware_Bar_Code)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Ware_Discount).HasDefaultValue((byte)100);
            entity.Property(e => e.Ware_Name)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Ware_No)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Ware_PY)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Ware_Price).HasColumnType("money");
            entity.Property(e => e.Ware_Type)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Ware_Unit)
                .HasMaxLength(8)
                .IsUnicode(false);
        });

        modelBuilder.Entity<XF_WareInfoPhoto>(entity =>
        {
            entity.HasKey(e => e.Ware_ID).HasName("PK__XF_WareI__D1D56E8B2818EA29");

            entity.ToTable("XF_WareInfoPhoto");

            entity.Property(e => e.Ware_ID).ValueGeneratedNever();
            entity.Property(e => e.Photo).HasColumnType("image");
        });

        modelBuilder.Entity<XF_WareType>(entity =>
        {
            entity.HasKey(e => e.Type_No).HasName("PK__XF_WareT__FE909ABC0E591826");

            entity.ToTable("XF_WareType");

            entity.Property(e => e.Type_No)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Remark)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Type_Name)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<grade>(entity =>
        {
            entity.HasKey(e => e.grade_id).HasName("PK__grades__3A8F732C3449B6E4");

            entity.Property(e => e.grade_id).ValueGeneratedNever();
            entity.Property(e => e.grade_code)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.grade_name).HasMaxLength(20);
            entity.Property(e => e.remark).HasMaxLength(300);
        });

        modelBuilder.Entity<school>(entity =>
        {
            entity.HasKey(e => e.school_id).HasName("PK__schools__27CA6CF42F8501C7");

            entity.Property(e => e.school_id).ValueGeneratedNever();
            entity.Property(e => e.address).HasMaxLength(255);
            entity.Property(e => e.contact_phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.created_at)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.remark).HasMaxLength(300);
            entity.Property(e => e.school_code)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.school_name).HasMaxLength(100);
        });

        modelBuilder.Entity<st_moc_upload_name_log>(entity =>
        {
            entity.HasKey(e => e.ID_KEY).HasName("PK__st_moc_u__2DF8989E3FBB6990");

            entity.ToTable("st_moc_upload_name_log");

            entity.HasIndex(e => e.person_id, "IX_st_moc_upload_name_log");

            entity.HasIndex(e => e.moc_no, "IX_st_moc_upload_name_log_moc_no");

            entity.Property(e => e.err_msg)
                .HasMaxLength(1000)
                .IsUnicode(false);
            entity.Property(e => e.moc_no)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.remark)
                .HasMaxLength(300)
                .IsUnicode(false);
            entity.Property(e => e.upload_time).HasColumnType("datetime");
        });

        modelBuilder.Entity<student_parent>(entity =>
        {
            entity.HasKey(e => e.bind_id).HasName("PK__student___71940A7B381A47C8");

            entity.ToTable("student_parent");

            entity.Property(e => e.bind_id).ValueGeneratedNever();
            entity.Property(e => e._class)
                .HasMaxLength(50)
                .HasColumnName("class");
            entity.Property(e => e.contract_id)
                .HasMaxLength(72)
                .IsUnicode(false);
            entity.Property(e => e.contract_status)
                .HasMaxLength(36)
                .IsUnicode(false);
            entity.Property(e => e.contract_time).HasColumnType("datetime");
            entity.Property(e => e.created_at)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.is_primary).HasDefaultValue(true);
            entity.Property(e => e.out_contract_code)
                .HasMaxLength(72)
                .IsUnicode(false);
            entity.Property(e => e.parent_openid)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.relation_type)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.remark)
                .HasMaxLength(300)
                .IsUnicode(false);
            entity.Property(e => e.updated_at)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
        });

        modelBuilder.Entity<user>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__user__3213E83F28D80438");

            entity.ToTable("user");

            entity.Property(e => e.id).ValueGeneratedNever();
            entity.Property(e => e.avatar).HasMaxLength(255);
            entity.Property(e => e.created_at)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.email).HasMaxLength(100);
            entity.Property(e => e.last_login).HasColumnType("datetime");
            entity.Property(e => e.mobile).HasMaxLength(20);
            entity.Property(e => e.openid).HasMaxLength(255);
            entity.Property(e => e.password).HasMaxLength(255);
            entity.Property(e => e.status).HasDefaultValue((byte)1);
            entity.Property(e => e.unionid).HasMaxLength(255);
            entity.Property(e => e.updated_at)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.username).HasMaxLength(50);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
