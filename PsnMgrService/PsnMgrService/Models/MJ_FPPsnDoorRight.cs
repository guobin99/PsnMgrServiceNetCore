using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_FPPsnDoorRight
{
    public long ID_KEY { get; set; }

    public string? Ctrl_No { get; set; }

    public int? Door_No { get; set; }

    public int? Person_ID { get; set; }

    public string? FP_Pwd { get; set; }

    public int? FP_TimeGroupNo { get; set; }

    public int? FP_Time_No1 { get; set; }

    public int? FP_Time_No2 { get; set; }

    public int? FP_Time_No3 { get; set; }

    public string? Remark { get; set; }
}
