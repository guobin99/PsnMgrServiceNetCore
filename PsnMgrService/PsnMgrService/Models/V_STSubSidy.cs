using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class V_STSubSidy
{
    public string? Card_No { get; set; }

    public string? Person_No { get; set; }

    public int Person_ID { get; set; }

    public DateTime Subsidy_Date { get; set; }

    public string User_No { get; set; } = null!;

    public decimal Subsidy_Fund { get; set; }

    public decimal? Plan_Fund { get; set; }

    public string? Dept_No { get; set; }

    public string? Dept_Name { get; set; }

    public string? Type_Name { get; set; }

    public string? Person_Name { get; set; }

    public int? Dept_ID { get; set; }

    public string? Type_No { get; set; }
}
