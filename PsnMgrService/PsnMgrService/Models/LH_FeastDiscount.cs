using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class LH_FeastDiscount
{
    public int ID_KEY { get; set; }

    public int Discount_Kind { get; set; }

    public string Feast_Date { get; set; } = null!;

    public int Discount_Num { get; set; }

    public string? Remark { get; set; }
}
