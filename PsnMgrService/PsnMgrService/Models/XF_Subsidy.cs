using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class XF_Subsidy
{
    public int ID_Key { get; set; }

    public int? Person_ID { get; set; }

    public string? Card_No { get; set; }

    public DateTime? Apply_Time { get; set; }

    public decimal? Subsidy_Fund { get; set; }

    public decimal? Card_Balance { get; set; }

    public byte? Apply_State { get; set; }

    public string? Moc_No { get; set; }
}
