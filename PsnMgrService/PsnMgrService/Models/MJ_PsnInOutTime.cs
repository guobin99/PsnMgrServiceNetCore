using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_PsnInOutTime
{
    public int PsnTime_No { get; set; }

    public string PsnTime_Name { get; set; } = null!;

    public string Start_Date { get; set; } = null!;

    public string? End_Date { get; set; }

    public byte Rule_Type { get; set; }

    public int? Cycle_Total { get; set; }

    public byte? SpecTime_Info { get; set; }

    public string? restweek { get; set; }

    public string? holiday { get; set; }

    public string? reststarttime1 { get; set; }

    public string? restendtime1 { get; set; }

    public string? reststarttime2 { get; set; }

    public string? restendtime2 { get; set; }

    public string? reststarttime3 { get; set; }

    public string? restendtime3 { get; set; }

    public string? reststarttime4 { get; set; }

    public string? restendtime4 { get; set; }

    public string? reststarttime5 { get; set; }

    public string? restendtime5 { get; set; }

    public string? reststarttime6 { get; set; }

    public string? restendtime6 { get; set; }

    public string? reststarttime7 { get; set; }

    public string? restendtime7 { get; set; }

    public string? reststarttime8 { get; set; }

    public string? restendtime8 { get; set; }

    public string? workstarttime1 { get; set; }

    public string? workendtime1 { get; set; }

    public string? workstarttime2 { get; set; }

    public string? workendtime2 { get; set; }

    public string? workstarttime3 { get; set; }

    public string? workendtime3 { get; set; }

    public string? workstarttime4 { get; set; }

    public string? workendtime4 { get; set; }

    public string? workstarttime5 { get; set; }

    public string? workendtime5 { get; set; }

    public string? workstarttime6 { get; set; }

    public string? workendtime6 { get; set; }

    public string? workstarttime7 { get; set; }

    public string? workendtime7 { get; set; }

    public string? workstarttime8 { get; set; }

    public string? workendtime8 { get; set; }
}
