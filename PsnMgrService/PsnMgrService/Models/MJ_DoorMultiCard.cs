using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_DoorMultiCard
{
    public string Ctrl_No { get; set; } = null!;

    public int Door_No { get; set; }

    public string Value_Type { get; set; } = null!;

    public string? Value { get; set; }
}
