using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_SysLog
{
    public int ID_KEY { get; set; }

    public string User_Name { get; set; } = null!;

    public DateTime Opt_Time { get; set; }

    public string? Opt_Computer { get; set; }

    public string? Opt_log { get; set; }

    public string System_No { get; set; } = null!;
}
