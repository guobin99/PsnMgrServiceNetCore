using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class V_MJAbnormRecord
{
    public DateTime BrushCard_Time { get; set; }

    public string Ctrl_No { get; set; } = null!;

    public string? Ctrl_Name { get; set; }

    public int? Door_No { get; set; }

    public string? Door_Name { get; set; }

    public string? Card_No { get; set; }

    public string? Person_No { get; set; }

    public string? Person_Name { get; set; }

    public string? Event_Name { get; set; }

    public int? Event_Code { get; set; }

    public bool Is_InDoor { get; set; }

    public string? Dept_No { get; set; }

    public string? Dept_Name { get; set; }
}
