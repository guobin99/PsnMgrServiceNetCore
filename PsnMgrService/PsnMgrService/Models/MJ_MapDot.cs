using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_MapDot
{
    public string Map_No { get; set; } = null!;

    public string Ctrl_No { get; set; } = null!;

    public int Door_No { get; set; }

    public int? Site_X { get; set; }

    public int? Site_Y { get; set; }
}
