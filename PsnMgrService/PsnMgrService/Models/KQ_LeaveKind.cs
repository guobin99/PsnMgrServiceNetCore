using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class KQ_LeaveKind
{
    public string Kind_No { get; set; } = null!;

    public string Kind_Name { get; set; } = null!;

    public string? Kind_Code { get; set; }

    public bool Is_Deduct { get; set; }

    public virtual ICollection<KQ_Leave> KQ_Leaves { get; set; } = new List<KQ_Leave>();
}
