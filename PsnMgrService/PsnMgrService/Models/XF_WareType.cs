using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class XF_WareType
{
    public string Type_No { get; set; } = null!;

    public string Type_Name { get; set; } = null!;

    public string? Remark { get; set; }

    public int Type_Mode { get; set; }
}
