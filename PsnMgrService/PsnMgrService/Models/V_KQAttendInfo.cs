using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class V_KQAttendInfo
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

    public string? Class_Name { get; set; }

    public string Person_Name { get; set; } = null!;

    public string Person_No { get; set; } = null!;

    public string Card_No { get; set; } = null!;

    public string? Dept_Name { get; set; }

    public string? Dept_No { get; set; }

    public int Dept_ID { get; set; }
}
