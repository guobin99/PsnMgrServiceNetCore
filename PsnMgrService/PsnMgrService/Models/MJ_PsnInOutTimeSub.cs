using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_PsnInOutTimeSub
{
    public int ID_KEY { get; set; }

    public int PsnTime_No { get; set; }

    public string Group_No { get; set; } = null!;

    public short TimeSect_ID { get; set; }

    public string Start_Time { get; set; } = null!;

    public string End_Time { get; set; } = null!;
}
