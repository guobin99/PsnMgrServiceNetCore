using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class DC_Repast
{
    public string Repast_No { get; set; } = null!;

    public string Bgn_Time { get; set; } = null!;

    public string End_Time { get; set; } = null!;

    public byte Order_Days { get; set; }

    public bool Can_Order { get; set; }
}
