using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class KQ_RuleClassSub
{
    public int ID_KEY { get; set; }

    public string Class_No { get; set; } = null!;

    public string Shift_ClassNo { get; set; } = null!;

    public int Shift_Days { get; set; }

    public virtual KQ_RuleClass Class_NoNavigation { get; set; } = null!;
}
