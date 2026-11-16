using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_PsnDoorRight
{
    public int Person_ID { get; set; }

    public string Ctrl_No { get; set; } = null!;

    public int Door_No { get; set; }

    public int? PsnTime_No { get; set; }

    public bool FirstCardOpen { get; set; }
}
