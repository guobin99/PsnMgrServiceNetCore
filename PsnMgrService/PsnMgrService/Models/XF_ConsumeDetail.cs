using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class XF_ConsumeDetail
{
    public long id_key { get; set; }

    public string? order_no { get; set; }

    public int? person_id { get; set; }

    public DateTime? consume_time { get; set; }

    public string? moc_no { get; set; }

    public int? ware_id { get; set; }

    public double? ware_copies { get; set; }

    public decimal? ware_before_price { get; set; }

    public decimal? ware_price { get; set; }

    public decimal? ware_funds { get; set; }

    public string? remark { get; set; }
}
