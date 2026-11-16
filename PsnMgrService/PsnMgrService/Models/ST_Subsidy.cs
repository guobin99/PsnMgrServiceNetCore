using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_Subsidy
{
    public int ID_KEY { get; set; }

    public int Person_ID { get; set; }

    public DateTime Subsidy_Date { get; set; }

    public decimal Subsidy_Fund { get; set; }

    public decimal? Plan_Fund { get; set; }

    public string User_No { get; set; } = null!;

    public string? Remark { get; set; }

    public int Data_Type { get; set; }

    public DateTime Create_Time { get; set; }

    public DateTime? Clear_Subsidy_BegTime { get; set; }

    public DateTime? Clear_Subsidy_EndTime { get; set; }

    public DateTime? Clear_Consume_BegTime { get; set; }

    public DateTime? Clear_Consume_EndTime { get; set; }
}
