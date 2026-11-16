using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class KQ_ModifyMonthly
{
    public int ID_KEY { get; set; }

    public int? Person_ID { get; set; }

    public double? MustWork_Hour { get; set; }

    public double? MustWork_Days { get; set; }

    public double? FactWork_Hour { get; set; }

    public double? FactWork_Days { get; set; }

    public double? AbsentWork_Hour { get; set; }

    public double? AbsentWork_Days { get; set; }

    public double? Overtime_Hour { get; set; }

    public double? Overtime_Days { get; set; }

    public double? Overrest_Hour { get; set; }

    public double? Overrest_Days { get; set; }

    public double? Overholiday_Hour { get; set; }

    public double? Overholiday_Days { get; set; }

    public double? Late1_Minute { get; set; }

    public byte? Late1_Times { get; set; }

    public double? Late2_Minute { get; set; }

    public byte? Late2_Times { get; set; }

    public double? Late3_Minute { get; set; }

    public byte? Late3_Times { get; set; }

    public double? Early1_Minute { get; set; }

    public byte? Early1_Times { get; set; }

    public double? Early2_Minute { get; set; }

    public byte? Early2_Times { get; set; }

    public double? Early3_Minute { get; set; }

    public byte? Early3_Times { get; set; }

    public double? TotalLate_Minute { get; set; }

    public byte? TotalLate_Times { get; set; }

    public double? TotalEarly_Minute { get; set; }

    public byte? TotalEarly_Times { get; set; }

    public double? J101_Hour { get; set; }

    public double? J101_Days { get; set; }

    public double? J102_Hour { get; set; }

    public double? J102_Days { get; set; }

    public double? J103_Hour { get; set; }

    public double? J103_Days { get; set; }

    public double? J104_Hour { get; set; }

    public double? J104_Days { get; set; }

    public double? J105_Hour { get; set; }

    public double? J105_Days { get; set; }

    public double? J106_Hour { get; set; }

    public double? J106_Days { get; set; }

    public double? J107_Hour { get; set; }

    public double? J107_Days { get; set; }

    public double? J108_Hour { get; set; }

    public double? J108_Days { get; set; }

    public double? J109_Hour { get; set; }

    public double? J109_Days { get; set; }

    public double? J110_Hour { get; set; }

    public double? J110_Days { get; set; }

    public double? J111_Hour { get; set; }

    public double? J111_Days { get; set; }

    public double? J112_Hour { get; set; }

    public double? J112_Days { get; set; }

    public double? J113_Hour { get; set; }

    public double? J113_Days { get; set; }

    public double? J114_Hour { get; set; }

    public double? J114_Days { get; set; }

    public double? J115_Hour { get; set; }

    public double? J115_Days { get; set; }

    public double? J116_Hour { get; set; }

    public double? J116_Days { get; set; }

    public double? J117_Hour { get; set; }

    public double? J117_Days { get; set; }

    public double? J118_Hour { get; set; }

    public double? J118_Days { get; set; }

    public double? TotalLeave_Hour { get; set; }

    public double? TotalLeave_Days { get; set; }

    public short? A { get; set; }

    public short? B { get; set; }

    public short? C { get; set; }

    public short? D { get; set; }

    public short? E { get; set; }

    public short? F { get; set; }

    public short? G { get; set; }

    public short? H { get; set; }

    public short? I { get; set; }

    public short? J { get; set; }

    public short? K { get; set; }

    public short? L { get; set; }

    public string? Account_Code { get; set; }
}
