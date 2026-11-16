using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_Solution
{
    public int Solution_ID { get; set; }

    public string Solution_No { get; set; } = null!;

    public string Solution_Name { get; set; } = null!;

    public string Ctrl_No { get; set; } = null!;

    public int Door_No { get; set; }

    public int? PsnTime_No { get; set; }
}
