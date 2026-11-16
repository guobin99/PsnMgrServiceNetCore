using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class KQ_Leave
{
    public int ID_KEY { get; set; }

    public int Person_ID { get; set; }

    public string? Kind_No { get; set; }

    public string? Leave_Date { get; set; }

    public double Leave_Hour { get; set; }

    public bool IsOutLeave { get; set; }

    public byte? Leave_Sect { get; set; }

    public string? Remark { get; set; }

    public string? User_No { get; set; }

    public string? Leave_TimeBgn { get; set; }

    public string? Leave_TimeEnd { get; set; }

    public DateTime? Update_Date { get; set; }

    public virtual KQ_LeaveKind? Kind_NoNavigation { get; set; }
}
