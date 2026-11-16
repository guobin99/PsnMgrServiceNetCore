using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class KQ_DefineClass
{
    public string Class_No { get; set; } = null!;

    public string? Class_Name { get; set; }

    public string? Remark { get; set; }

    public string? User_No { get; set; }

    public DateTime? Update_Date { get; set; }

    public virtual ICollection<KQ_DefineClassSub> KQ_DefineClassSubs { get; set; } = new List<KQ_DefineClassSub>();
}
