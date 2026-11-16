using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_DoorSwitchTimeSub
{
    public int DoorTime_No { get; set; }

    public string Start_Time { get; set; } = null!;

    public string End_Time { get; set; } = null!;

    public byte Door_State { get; set; }
}
