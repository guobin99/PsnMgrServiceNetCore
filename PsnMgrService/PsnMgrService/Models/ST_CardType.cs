using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_CardType
{
    public string Type_No { get; set; } = null!;

    public string Type_Name { get; set; } = null!;

    public decimal Min_Balance { get; set; }

    public decimal Subsidy { get; set; }

    public int Validity { get; set; }

    public string? Use_MOC { get; set; }

    public decimal? SF_Max_Fund { get; set; }

    public decimal? SF_Max_DayFund { get; set; }

    public int SF_LimitTimes { get; set; }

    public decimal Max_IcFunds { get; set; }

    public int? SF_Max_Count { get; set; }

    public int? SF_Discount { get; set; }

    public decimal? LH_Max_Fund { get; set; }

    public decimal? LH_Max_DayFund { get; set; }

    public int? LH_Discount { get; set; }

    public string? Use_Repast { get; set; }

    public int? Small_Discount { get; set; }

    public bool Is_Del { get; set; }

    public bool Is_OnlySubsidy { get; set; }

    public string? Remark { get; set; }

    public string? type_sign { get; set; }
}
