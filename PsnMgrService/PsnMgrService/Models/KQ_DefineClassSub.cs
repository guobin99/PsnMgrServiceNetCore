using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class KQ_DefineClassSub
{
    public int ID_KEY { get; set; }

    public string Class_No { get; set; } = null!;

    public string Worktime_Date { get; set; } = null!;

    public string? OfWeek { get; set; }

    public string? Day_ClassNo { get; set; }

    public virtual KQ_DefineClass Class_NoNavigation { get; set; } = null!;
}
