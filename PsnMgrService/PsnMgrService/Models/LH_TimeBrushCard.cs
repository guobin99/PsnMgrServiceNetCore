using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class LH_TimeBrushCard
{
    public string Card_No { get; set; } = null!;

    public DateTime Brush_Time { get; set; }
}
