using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class KQ_AdjustClassHistory
{
    public int ID_KEY { get; set; }

    public int Person_ID { get; set; }

    public string? Adjust_Date { get; set; }

    public string Class_No { get; set; } = null!;

    public string? Remark { get; set; }

    public string? User_No { get; set; }

    public DateTime? Update_Date { get; set; }
}
