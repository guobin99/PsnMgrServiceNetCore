using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class KQ_Daily
{
    public int ID_KEY { get; set; }

    public int? Person_ID { get; set; }

    public string? Worktime_Date { get; set; }

    public string? OfWeek { get; set; }

    public string Class_No { get; set; } = null!;

    public string? Class_Data { get; set; }

    public string? Sect1_InFact { get; set; }

    public string? Sect1_OutFact { get; set; }

    public string? Sect2_InFact { get; set; }

    public string? Sect2_OutFact { get; set; }

    public string? Sect3_InFact { get; set; }

    public string? Sect3_OutFact { get; set; }

    public string? Sect4_InFact { get; set; }

    public string? Sect4_OutFact { get; set; }

    public string? Sect5_InFact { get; set; }

    public string? Sect5_OutFact { get; set; }

    public double? Must_Work { get; set; }

    public double? Fact_Work { get; set; }

    public double? Absent_Work { get; set; }

    public string? Reason { get; set; }

    public double? Leave_Hour { get; set; }

    public string? Leave_Data { get; set; }

    public string? Leave_Name { get; set; }

    public double? Overtime_Hour { get; set; }

    public string? Overtime_Class { get; set; }

    public double? Late_Minute { get; set; }

    public string? Late_Data { get; set; }

    public double? Early_Minute { get; set; }

    public string? Early_Data { get; set; }

    public string Brush_Data { get; set; } = null!;
}
