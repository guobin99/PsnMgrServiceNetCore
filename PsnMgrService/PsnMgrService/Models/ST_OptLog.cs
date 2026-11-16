using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_OptLog
{
    public int ID_KEY { get; set; }

    public string User_Name { get; set; } = null!;

    public string Opt_Computer { get; set; } = null!;

    public string System_No { get; set; } = null!;

    public string Opt_Table { get; set; } = null!;

    public string Opt_Type { get; set; } = null!;

    public DateTime Opt_Time { get; set; }

    public string Opt_Info { get; set; } = null!;

    public string? Remark { get; set; }
}
