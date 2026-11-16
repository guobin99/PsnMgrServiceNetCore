using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class V_XFPsnConsume
{
    public int Person_ID { get; set; }

    public string System_No { get; set; } = null!;

    public int Consume_Mode { get; set; }

    public DateTime Consume_Time { get; set; }

    public decimal Consume_Fund { get; set; }

    public int? Consume_Copy { get; set; }

    public double? Consume_TimeLen { get; set; }

    public double? Consume_Capacity { get; set; }

    public string? Repast_No { get; set; }

    public int? Ware_ID { get; set; }

    public string MOC_No { get; set; } = null!;

    public string? COM_No { get; set; }

    public decimal Card_Balance { get; set; }

    public decimal? DB_Balance { get; set; }

    public decimal? Card_SmallAccount { get; set; }

    public byte Data_Type { get; set; }

    public string? Remark { get; set; }

    public string? Person_NO { get; set; }

    public string? Person_Name { get; set; }

    public int? Dept_ID { get; set; }

    public string? Dept_NO { get; set; }

    public string? Dept_Name { get; set; }

    public string? MOC_Name { get; set; }
}
