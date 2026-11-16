using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class LH_TimePartDiscount
{
    public int ID_KEY { get; set; }

    public int Discount_Kind { get; set; }

    public string Bgn_Time { get; set; } = null!;

    public string End_Time { get; set; } = null!;

    public int Discount_Num { get; set; }

    public string? Remark { get; set; }
}
