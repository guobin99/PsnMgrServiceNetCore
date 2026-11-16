using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class V_MJDotLinkage
{
    public string Linkage_No { get; set; } = null!;

    public string Linkage_Name { get; set; } = null!;

    public string Ctrl_No { get; set; } = null!;

    public string? Ctrl_Name { get; set; }

    public string? Ctrl_Type { get; set; }

    public string Dot_No { get; set; } = null!;

    public string? Dot_Name { get; set; }

    public bool? Is_Input { get; set; }

    public byte? Dot_State { get; set; }
}
