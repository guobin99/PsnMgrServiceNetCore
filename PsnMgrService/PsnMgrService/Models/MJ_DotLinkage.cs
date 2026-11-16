using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_DotLinkage
{
    public string Ctrl_No { get; set; } = null!;

    public string Linkage_No { get; set; } = null!;

    public string Dot_No { get; set; } = null!;

    public string Linkage_Name { get; set; } = null!;

    public bool? Is_Input { get; set; }

    public byte? Dot_State { get; set; }
}
