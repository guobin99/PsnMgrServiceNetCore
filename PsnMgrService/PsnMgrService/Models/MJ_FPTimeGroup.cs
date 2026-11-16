using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_FPTimeGroup
{
    public int TimeGroup_No { get; set; }

    public string? TimeGroup_Name { get; set; }

    public int? Time_No1 { get; set; }

    public int? Time_No2 { get; set; }

    public int? Time_No3 { get; set; }

    public string? Remark { get; set; }
}
