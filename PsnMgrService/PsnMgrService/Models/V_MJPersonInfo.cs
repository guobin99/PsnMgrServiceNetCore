using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class V_MJPersonInfo
{
    public int Person_ID { get; set; }

    public string? Person_No { get; set; }

    public string? Person_Name { get; set; }

    public string? Card_No { get; set; }

    public string? OnWork_Date { get; set; }

    public string? Door_Pwd { get; set; }

    public byte? Person_State { get; set; }

    public int? Dept_ID { get; set; }

    public string? Dept_No { get; set; }

    public string? Dept_Name { get; set; }
}
