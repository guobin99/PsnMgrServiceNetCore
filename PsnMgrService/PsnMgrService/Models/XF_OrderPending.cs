using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class XF_OrderPending
{
    public int ID_KEY { get; set; }

    public string Order_No { get; set; } = null!;

    public string? Moc_No { get; set; }

    public DateTime Pending_Time { get; set; }

    public int? Person_ID { get; set; }

    public decimal? Ware_All_Funds { get; set; }

    public int? Ware_All_Copies { get; set; }

    public string? Remark { get; set; }
}
