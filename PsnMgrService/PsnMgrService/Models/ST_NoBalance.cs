using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_NoBalance
{
    public int NoBalance_ID { get; set; }

    public string? Card_No { get; set; }

    public decimal? Card_Balance { get; set; }

    public decimal? DataBase_Balance { get; set; }

    public decimal? Adjust_Fund { get; set; }

    public string? Opt_Project { get; set; }

    public DateTime? Opt_Time { get; set; }

    public string User_No { get; set; } = null!;

    public string? Remark { get; set; }
}
