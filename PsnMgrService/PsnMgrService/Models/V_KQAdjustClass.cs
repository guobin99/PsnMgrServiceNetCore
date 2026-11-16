using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class V_KQAdjustClass
{
    public int ID_KEY { get; set; }

    public int Person_ID { get; set; }

    public string? Adjust_Date { get; set; }

    public string Class_No { get; set; } = null!;

    public string? Remark { get; set; }

    public string? User_No { get; set; }

    public DateTime? Update_Date { get; set; }

    public string? Person_No { get; set; }

    public string? Person_Name { get; set; }

    public string? Card_No { get; set; }

    public string? Dept_No { get; set; }

    public string? Dept_Name { get; set; }

    public int? Dept_ID { get; set; }

    public string? Class_Name { get; set; }
}
