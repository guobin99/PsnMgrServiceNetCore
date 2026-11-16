using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class KQ_DefineClassSubHistory
{
    public int ID_KEY { get; set; }

    public string Class_No { get; set; } = null!;

    public string? Day_ClassNo { get; set; }

    public string Worktime_Date { get; set; } = null!;

    public string? OfWeek { get; set; }
}
