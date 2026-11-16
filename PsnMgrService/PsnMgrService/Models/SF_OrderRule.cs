using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class SF_OrderRule
{
    public int id_Key { get; set; }

    public string? Moc_No { get; set; }

    public string? Sk_BeginTime { get; set; }

    public string? Sk_EndTime { get; set; }

    public int? Order_Kind { get; set; }

    public string? Order_Repast { get; set; }

    public string? Note { get; set; }
}
