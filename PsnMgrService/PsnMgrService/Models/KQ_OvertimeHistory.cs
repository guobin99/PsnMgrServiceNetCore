using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class KQ_OvertimeHistory
{
    public int ID_KEY { get; set; }

    public int Person_ID { get; set; }

    public string? Over_Date { get; set; }

    public double Over_Hour { get; set; }

    public string? Remark { get; set; }

    public string? User_No { get; set; }

    public DateTime? Update_Date { get; set; }
}
