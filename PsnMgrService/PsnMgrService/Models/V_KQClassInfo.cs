using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class V_KQClassInfo
{
    public string Class_Type { get; set; } = null!;

    public string Class_No { get; set; } = null!;

    public string? Class_Name { get; set; }
}
