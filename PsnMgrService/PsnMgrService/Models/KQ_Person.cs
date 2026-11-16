using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class KQ_Person
{
    public int Person_ID { get; set; }

    public string Class_No { get; set; } = null!;

    public byte? Worktime_Mode { get; set; }

    public byte? Overtime_Mode { get; set; }

    public byte? Rest_Mode { get; set; }

    public string? Rest_Days { get; set; }

    public bool? IsHolidayAsWorkTime { get; set; }

    public bool? IsOverTimeAsWorkTime { get; set; }

    public byte Person_State { get; set; }

    public string? Remark { get; set; }

    public string? User_No { get; set; }

    public DateTime? Update_Date { get; set; }
}
