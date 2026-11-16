using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class XF_OrderPendingSub
{
    public int ID_KEY { get; set; }

    public string Order_No { get; set; } = null!;

    public int? Ware_ID { get; set; }

    public int? Ware_Copies { get; set; }

    public decimal? Ware_Price { get; set; }

    public decimal? Ware_Funds { get; set; }

    public string? Remark { get; set; }
}
