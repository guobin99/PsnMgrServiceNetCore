using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class LH_SetParamCard
{
    public byte Mny_Index { get; set; }

    public byte Week_No { get; set; }

    public string Bgn_Time { get; set; } = null!;

    public string End_Time { get; set; } = null!;

    public decimal Unit_Fund { get; set; }
}
