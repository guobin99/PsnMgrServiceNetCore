using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class V_KQSupplyTimeHistory
{
    public int ID_KEY { get; set; }

    public int Person_ID { get; set; }

    public string? Supply_Date { get; set; }

    public string Supply_Time { get; set; } = null!;

    public string? Supply_Reason { get; set; }

    public short Supply_Class { get; set; }

    public string? Remark { get; set; }

    public string? User_No { get; set; }

    public DateTime? Update_Date { get; set; }

    public string? Person_No { get; set; }

    public string? Person_Name { get; set; }

    public string? Card_No { get; set; }

    public string? Dept_No { get; set; }

    public string? Dept_Name { get; set; }

    public int? Dept_ID { get; set; }
}
