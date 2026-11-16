using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class DC_OrderDatum
{
    public int ID_KEY { get; set; }

    public int Person_ID { get; set; }

    public string Order_Date { get; set; } = null!;

    public DateTime Order_Time { get; set; }

    public string Repast_No { get; set; } = null!;

    public string Moc_No_DC { get; set; } = null!;

    public byte Data_Type { get; set; }

    public DateTime? Download_Time { get; set; }

    public DateTime? Consume_Time { get; set; }

    public string? Moc_No_SF { get; set; }

    public string? Remark { get; set; }

    public decimal? Order_Fund { get; set; }

    public decimal? Order_BeforeFund { get; set; }

    public bool Is_WareOrder { get; set; }

    public string? Order_No { get; set; }

    public string? Telephone { get; set; }

    public string? Delivery_Addr { get; set; }

    public string? Taste { get; set; }

    public int? copies { get; set; }
}
