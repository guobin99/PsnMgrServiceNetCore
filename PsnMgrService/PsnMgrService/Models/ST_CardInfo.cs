using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_CardInfo
{
    public string Card_Type { get; set; } = null!;

    public decimal Card_Foregift { get; set; }

    public decimal Card_Charge { get; set; }

    public string? Passwd_Value { get; set; }

    public decimal SmallAccount_Max { get; set; }

    public DateTime? Subsidy_CutOffDate { get; set; }

    public bool Is_DelSubsidy { get; set; }

    public int? Day_DelSysBlack { get; set; }

    public short Card_Spec { get; set; }

    public string? Init_Code { get; set; }
}
