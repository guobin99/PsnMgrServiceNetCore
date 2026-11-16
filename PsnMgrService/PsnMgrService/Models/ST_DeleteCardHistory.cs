using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_DeleteCardHistory
{
    public int ID_KEY { get; set; }

    public int Person_ID { get; set; }

    public DateTime New_Date { get; set; }

    public DateTime Del_Date { get; set; }

    public string Card_No { get; set; } = null!;

    public decimal? Back_Fund { get; set; }

    public decimal Fact_Fund { get; set; }

    public decimal? Back_SmallFund { get; set; }

    public decimal? Back_Foregift { get; set; }

    public decimal? Card_Charge { get; set; }

    public string User_No { get; set; } = null!;

    public string New_User_No { get; set; } = null!;

    public string? Remark { get; set; }
}
