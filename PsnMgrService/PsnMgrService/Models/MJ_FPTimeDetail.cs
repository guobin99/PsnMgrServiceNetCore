using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_FPTimeDetail
{
    public int Time_No { get; set; }

    public byte Week_No { get; set; }

    public DateTime? Bgn_Time { get; set; }

    public DateTime? End_Time { get; set; }

    public string? Remark { get; set; }
}
