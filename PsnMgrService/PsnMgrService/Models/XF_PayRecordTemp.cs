using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class XF_PayRecordTemp
{
    public int ID_Key { get; set; }

    public int? Person_id { get; set; }

    public string? Card_No { get; set; }

    public string? OrderID { get; set; }

    public string? BarCode { get; set; }

    public decimal? Consume_Fund { get; set; }

    public DateTime? Consume_Time { get; set; }

    public int? Pay_state { get; set; }

    public string? recv_info { get; set; }

    public string? System_No { get; set; }

    public int? Consume_Mode { get; set; }

    public decimal? Before_Fund { get; set; }

    public string? Repast_No { get; set; }

    public string? Ware_ID { get; set; }

    public string? Moc_No { get; set; }

    public decimal? Subsidy_Fund { get; set; }

    public string? pay_type { get; set; }
}
