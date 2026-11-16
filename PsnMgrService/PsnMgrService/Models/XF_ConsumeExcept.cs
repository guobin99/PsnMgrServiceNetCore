using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class XF_ConsumeExcept
{
    public long ID_KEY { get; set; }

    public string? Card_no { get; set; }

    public int? Consume_Mode { get; set; }

    public string? Consume_Time { get; set; }

    public decimal? Consume_Fund { get; set; }

    public int? Consume_Copy { get; set; }

    public double? Consume_Capacity { get; set; }

    public string? Ware_No { get; set; }

    public string MOC_No { get; set; } = null!;

    public decimal? Card_Balance { get; set; }

    public DateTime? Download_Time { get; set; }

    public int? Action { get; set; }

    public string? Remark { get; set; }

    public string? info1 { get; set; }

    public string? info2 { get; set; }

    public string? info3 { get; set; }
}
