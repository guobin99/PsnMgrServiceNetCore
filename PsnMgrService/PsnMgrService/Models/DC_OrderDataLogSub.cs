using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class DC_OrderDataLogSub
{
    public int ID_KEY { get; set; }

    public int Ware_ID { get; set; }

    public string? Order_No { get; set; }

    public int Ware_Copies { get; set; }

    public decimal Ware_Price { get; set; }

    public decimal Ware_BeforeFund { get; set; }

    public decimal Ware_Fund { get; set; }

    public string? Ware_Taste { get; set; }

    public string? Remark { get; set; }
}
