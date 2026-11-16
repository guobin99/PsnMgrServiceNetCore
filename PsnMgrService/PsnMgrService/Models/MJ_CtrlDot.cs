using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_CtrlDot
{
    public string Ctrl_No { get; set; } = null!;

    public string Dot_No { get; set; } = null!;

    public string Dot_Name { get; set; } = null!;

    public byte Dot_Type { get; set; }

    public byte? Dot_State { get; set; }

    public bool? Is_Input { get; set; }

    public bool? Is_Extend { get; set; }

    public bool? Is_Auto { get; set; }
}
