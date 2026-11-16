using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class V_STPersonCard
{
    public int Person_ID { get; set; }

    public int? Dept_ID { get; set; }

    public string Card_No { get; set; } = null!;

    public string Person_No { get; set; } = null!;

    public string Person_Name { get; set; } = null!;

    public string? Dept_Name { get; set; }

    public string? Type_No { get; set; }

    public string? Card_Password { get; set; }

    public DateTime? Card_NewDate { get; set; }

    public DateTime? Card_ValidDate { get; set; }

    public byte? Card_State { get; set; }

    public decimal? Card_SmallBalance { get; set; }

    public decimal? Card_Balance { get; set; }

    public decimal? Subsidy_Fund { get; set; }

    public string? Type_Name { get; set; }

    public string? Dept_No { get; set; }

    public int? Small_Discount { get; set; }

    public decimal? Max_IcFunds { get; set; }

    public string? OffWork_Date { get; set; }
}
