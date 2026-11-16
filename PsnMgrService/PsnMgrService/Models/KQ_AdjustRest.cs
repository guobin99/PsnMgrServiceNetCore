using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class KQ_AdjustRest
{
    public int ID_KEY { get; set; }

    public int Person_ID { get; set; }

    public string? Adjust_Date { get; set; }

    public byte Adjust_Class { get; set; }

    public double Adjust_Hour { get; set; }

    public string? Remark { get; set; }

    public string? User_No { get; set; }

    public DateTime? Update_Date { get; set; }
}
