using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_ControlInfo
{
    public string Ctrl_No { get; set; } = null!;

    public int Door_No { get; set; }

    public int Person_ID { get; set; }

    public string? Card_No { get; set; }

    public string? Description { get; set; }

    public byte? Door_State { get; set; }

    public byte? Record_Color { get; set; }

    public DateTime? Run_Time { get; set; }

    public string? Run_Info { get; set; }

    public string? Alarm_Info { get; set; }

    public string? State_Info { get; set; }

    public DateTime? Update_Time { get; set; }
}
