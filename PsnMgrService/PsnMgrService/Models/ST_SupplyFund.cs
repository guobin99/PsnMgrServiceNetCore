using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_SupplyFund
{
    public int ID_KEY { get; set; }

    public int Person_ID { get; set; }

    public DateTime Supply_Date { get; set; }

    public decimal Supply_Fund { get; set; }

    public decimal? Card_Balance { get; set; }

    public string? MOC_No { get; set; }

    public byte Data_Type { get; set; }

    public string User_No { get; set; } = null!;

    public string? Remark { get; set; }

    public string? Out_Trade_No { get; set; }
}
