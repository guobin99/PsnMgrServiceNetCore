using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class KQ_RuleClass
{
    public string Class_No { get; set; } = null!;

    public string? Class_Name { get; set; }

    public string? Start_Date { get; set; }

    public byte? Rule_Mode { get; set; }

    public short Rule_Cycle { get; set; }

    public string? Remark { get; set; }

    public string? User_No { get; set; }

    public DateTime? Update_Date { get; set; }

    public virtual ICollection<KQ_RuleClassSub> KQ_RuleClassSubs { get; set; } = new List<KQ_RuleClassSub>();
}
