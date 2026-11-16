using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_PersonAccount
{
    public int Person_ID { get; set; }

    public decimal Card_Foregift { get; set; }

    public decimal Card_Charge { get; set; }

    public decimal Supply_Fund { get; set; }

    public decimal Subsidy_Fund { get; set; }

    public decimal Take_Fund { get; set; }

    public decimal AlterCard_Fund { get; set; }

    public decimal Transfer_Fund { get; set; }

    public decimal Account_Balance { get; set; }

    public decimal Back_Foregift { get; set; }

    public decimal Back_Fund { get; set; }

    public decimal Back_SmallFund { get; set; }

    public decimal EM_ConsumeFund { get; set; }

    public decimal XF_ConsumeFund { get; set; }

    public string? Remark { get; set; }
}
