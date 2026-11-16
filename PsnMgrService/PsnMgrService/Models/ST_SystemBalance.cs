using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_SystemBalance
{
    public DateTime Balance_Time { get; set; }

    public decimal Supply_AllFund { get; set; }

    public decimal Take_AllFund { get; set; }

    public decimal Card_Foregift { get; set; }

    public decimal Card_Charge { get; set; }

    public decimal Subsidy_AllFund { get; set; }

    public decimal Card_AllBalance { get; set; }

    public decimal Transfer_AllFund { get; set; }

    public decimal Consume_AllFund { get; set; }

    public decimal Remain_AllForegift { get; set; }

    public decimal Back_Fund { get; set; }

    public decimal Back_Foregift { get; set; }

    public decimal System_Balance { get; set; }

    public string? Remark { get; set; }
}
