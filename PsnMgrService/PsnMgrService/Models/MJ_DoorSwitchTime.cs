using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_DoorSwitchTime
{
    public int DoorTime_No { get; set; }

    public string DoorTime_Name { get; set; } = null!;

    public byte? SpecTime_Info { get; set; }
}
