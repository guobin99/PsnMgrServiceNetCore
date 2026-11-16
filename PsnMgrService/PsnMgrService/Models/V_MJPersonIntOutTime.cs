using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class V_MJPersonIntOutTime
{
    public int Person_ID { get; set; }

    public string? Person_No { get; set; }

    public string Ctrl_No { get; set; } = null!;

    public string? Ctrl_Name { get; set; }

    public int Door_No { get; set; }

    public string? Door_Name { get; set; }

    public int? PsnTime_No { get; set; }

    public string? PsnTime_Name { get; set; }
}
