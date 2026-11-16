using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class V_STAlterCard
{
    public string? Person_No { get; set; }

    public int Person_ID { get; set; }

    public DateTime Alter_Date { get; set; }

    public string User_No { get; set; } = null!;

    public string Card_No { get; set; } = null!;

    public string? NewCard_No { get; set; }

    public string? Dept_No { get; set; }

    public string? Dept_Name { get; set; }

    public string? Person_Name { get; set; }

    public int? Dept_ID { get; set; }

    public byte Alter_Type { get; set; }

    public decimal? Alter_Fee { get; set; }
}
