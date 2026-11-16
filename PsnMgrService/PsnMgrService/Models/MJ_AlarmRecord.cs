using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_AlarmRecord
{
    public int ID_KEY { get; set; }

    public DateTime Alarm_Time { get; set; }

    public string Ctrl_No { get; set; } = null!;

    public string? Dot_Name { get; set; }

    public int? Event_Code { get; set; }

    public bool Is_InDoor { get; set; }
}
