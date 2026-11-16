using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_DoorSpecTime
{
    public int ID_KEY { get; set; }

    public int SpecTime_No { get; set; }

    public string Start_Time { get; set; } = null!;

    public string End_Time { get; set; } = null!;

    public byte Door_State { get; set; }
}
