using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_SubSysRight
{
    public int Person_ID { get; set; }

    public string System_No { get; set; } = null!;

    public string? Remark { get; set; }
}
