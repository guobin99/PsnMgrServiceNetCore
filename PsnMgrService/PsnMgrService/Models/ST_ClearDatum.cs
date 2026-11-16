using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_ClearDatum
{
    public int Person_ID { get; set; }

    public decimal? Card_Foregift { get; set; }

    public decimal? Card_Charge { get; set; }

    public decimal? Supply_Fund { get; set; }

    public decimal? Subsidy_Fund { get; set; }

    public decimal? Fund { get; set; }

    public decimal? Take_Fund { get; set; }

    public decimal? Back_Fund { get; set; }

    public decimal? Back_Foregift { get; set; }

    public decimal? Transfer_Fund { get; set; }

    public string? Remark { get; set; }
}
