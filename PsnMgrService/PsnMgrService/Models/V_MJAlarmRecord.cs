using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class V_MJAlarmRecord
{
    public DateTime Alarm_Time { get; set; }

    public string Ctrl_No { get; set; } = null!;

    public string? Ctrl_Name { get; set; }

    public string? Dot_Name { get; set; }

    public string? Event_Name { get; set; }

    public int? Event_Code { get; set; }
}
