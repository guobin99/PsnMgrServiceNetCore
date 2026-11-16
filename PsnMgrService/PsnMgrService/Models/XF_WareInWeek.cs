using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class XF_WareInWeek
{
    public int ID_KEY { get; set; }

    public DateTime? Ware_Date { get; set; }

    public int? Ware_Week { get; set; }

    public int? Ware_ID { get; set; }

    public string? Repast_No { get; set; }

    public string? Remark { get; set; }

    public string? Area_No { get; set; }

    public int? Order_Copies { get; set; }
}
