using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class KQ_FillAttendHistory
{
    public int ID_KEY { get; set; }

    public int Person_ID { get; set; }

    public string? Fill_Date { get; set; }

    public byte Fill_Class { get; set; }

    public double Fill_Hour { get; set; }

    public string? Remark { get; set; }

    public string? User_No { get; set; }

    public DateTime? Update_Date { get; set; }
}
