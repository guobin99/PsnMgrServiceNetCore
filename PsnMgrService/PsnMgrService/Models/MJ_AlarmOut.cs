using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_AlarmOut
{
    public string Ctrl_No { get; set; } = null!;

    public byte Idx_No { get; set; }

    public byte Door_Info { get; set; }

    public byte OutputDot_No { get; set; }

    public byte Output_Time { get; set; }

    public int Event_Open { get; set; }

    public int Event_Alarm { get; set; }

    public int Event_Close { get; set; }
}
