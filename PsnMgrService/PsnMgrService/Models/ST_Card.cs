using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_Card
{
    public int Person_ID { get; set; }

    public string Type_No { get; set; } = null!;

    public string? Card_Password { get; set; }

    public DateTime Card_NewDate { get; set; }

    public DateTime Card_ValidDate { get; set; }

    public decimal Card_Foregift { get; set; }

    public decimal Card_Charge { get; set; }

    public decimal Card_Balance { get; set; }

    public decimal? Card_SmallBalance { get; set; }

    public byte? Card_State { get; set; }

    public string User_No { get; set; } = null!;

    public decimal? Subsidy_Fund { get; set; }

    public string? Remark { get; set; }

    public DateTime? Update_Time { get; set; }

    public decimal? IC_Balance { get; set; }

    public byte? Subsidy_Flag { get; set; }
}
