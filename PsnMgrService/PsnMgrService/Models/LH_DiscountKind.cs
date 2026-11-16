using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class LH_DiscountKind
{
    public int Discount_Kind { get; set; }

    public string Discount_Name { get; set; } = null!;

    public decimal? Default_ConsumeFund { get; set; }

    public int? Alarm_OverTime { get; set; }

    public int? Discount_Option { get; set; }

    public bool? Is_LeastConsume { get; set; }

    public decimal? LeastConsume_Fund { get; set; }

    public string? Remark { get; set; }
}
