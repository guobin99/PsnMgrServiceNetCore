using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class V_KQOvertime
{
    public int ID_KEY { get; set; }

    public int Person_ID { get; set; }

    public string? Over_Date { get; set; }

    public double Over_Hour { get; set; }

    public string? User_No { get; set; }

    public string? Remark { get; set; }

    public DateTime? Update_Date { get; set; }

    public string? Person_No { get; set; }

    public string? Card_No { get; set; }

    public string? Person_Name { get; set; }

    public int? Dept_ID { get; set; }

    public string? Dept_Name { get; set; }

    public string? Dept_No { get; set; }
}
