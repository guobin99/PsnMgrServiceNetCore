using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class LH_WeekDiscount
{
    public int ID_KEY { get; set; }

    public int Discount_Kind { get; set; }

    public int Week_No { get; set; }

    public int Discount_Num { get; set; }

    public string? Remark { get; set; }
}
