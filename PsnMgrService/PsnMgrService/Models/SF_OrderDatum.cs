using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class SF_OrderDatum
{
    public int id_Key { get; set; }

    public int? Person_ID { get; set; }

    public string? Card_No { get; set; }

    public string? Order_date { get; set; }

    public string? Repast_No { get; set; }

    public string? Moc_No { get; set; }

    public string? Note { get; set; }

    public DateTime update_time { get; set; }

    public DateTime Order_Time { get; set; }

    public decimal? Order_Fund { get; set; }

    public decimal? Order_BeforeFund { get; set; }

    public bool Is_WareOrder { get; set; }

    public string? Order_No { get; set; }

    public string? Delivery_Addr { get; set; }

    public string? Telephone { get; set; }

    public string? Taste { get; set; }

    public int? copies { get; set; }
}
