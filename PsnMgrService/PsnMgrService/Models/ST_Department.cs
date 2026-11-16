using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_Department
{
    public int Dept_ID { get; set; }

    public string? Dept_No { get; set; }

    public string? Dept_Name { get; set; }

    public string? Dept_Chief { get; set; }

    public int? Dept_Total { get; set; }

    public string? Dept_Duty { get; set; }

    public bool? Is_Del { get; set; }

    public string? Remark { get; set; }

    public string? Add_Col1 { get; set; }

    public string? Add_Col2 { get; set; }

    public int? Dept_Sort { get; set; }

    public string? Dept_Telphone { get; set; }

    public DateTime? Create_Time { get; set; }
}
