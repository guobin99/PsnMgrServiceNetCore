using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class SF_Repast
{
    public string Repast_No { get; set; } = null!;

    public string Repast_Name { get; set; } = null!;

    public string Bgn_Time { get; set; } = null!;

    public string End_Time { get; set; } = null!;

    public decimal Price { get; set; }

    public string? Remark { get; set; }

    public int limit_times { get; set; }

    public decimal limit_funds { get; set; }

    public decimal second_price { get; set; }
}
