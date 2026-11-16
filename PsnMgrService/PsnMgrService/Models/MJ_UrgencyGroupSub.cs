using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_UrgencyGroupSub
{
    public string UrgGrp_No { get; set; } = null!;

    public string Ctrl_No { get; set; } = null!;

    public int Door_No { get; set; }
}
