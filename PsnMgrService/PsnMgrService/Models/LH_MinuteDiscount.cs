using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class LH_MinuteDiscount
{
    public int ID_KEY { get; set; }

    public int Discount_Kind { get; set; }

    public int Bgn_Minute { get; set; }

    public int End_Minute { get; set; }

    public int Discount_Minute { get; set; }

    public string? Remark { get; set; }
}
