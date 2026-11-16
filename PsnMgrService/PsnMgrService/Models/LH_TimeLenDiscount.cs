using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class LH_TimeLenDiscount
{
    public int ID_KEY { get; set; }

    public int Discount_Kind { get; set; }

    public double Bgn_TimeLen { get; set; }

    public double End_TimeLen { get; set; }

    public int Discount_Num { get; set; }

    public string? Remark { get; set; }
}
