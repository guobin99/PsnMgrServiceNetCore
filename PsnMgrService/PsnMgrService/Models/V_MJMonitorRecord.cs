using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class V_MJMonitorRecord
{
    public DateTime? Run_Time { get; set; }

    public string Ctrl_No { get; set; } = null!;

    public string? Ctrl_Name { get; set; }

    public int Door_No { get; set; }

    public string? Door_Name { get; set; }

    public string? Card_No { get; set; }

    public string? Person_No { get; set; }

    public string? Person_Name { get; set; }

    public string? Dept_No { get; set; }

    public string? Dept_Name { get; set; }

    public string? Description { get; set; }

    public byte? Door_State { get; set; }

    public string? Run_Info { get; set; }

    public string? Alarm_Info { get; set; }

    public string? State_Info { get; set; }
}
