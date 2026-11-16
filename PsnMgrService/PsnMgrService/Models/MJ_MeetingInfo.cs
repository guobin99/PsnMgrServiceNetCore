using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_MeetingInfo
{
    public int ID_Key { get; set; }

    public string Ctrl_No { get; set; } = null!;

    public string? Door_No { get; set; }

    public string? Bgn_Date { get; set; }

    public string? Bgn_Time { get; set; }

    public string? End_Date { get; set; }

    public string? End_Time { get; set; }

    public string? Remark { get; set; }
}
