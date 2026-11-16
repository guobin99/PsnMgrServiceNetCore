using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class KQ_RealInfo
{
    public int ID_KEY { get; set; }

    public string? Card_No { get; set; }

    public DateTime? Brush_Time { get; set; }

    public string? IP_Addr { get; set; }

    public byte[]? Real_Image { get; set; }

    public DateTime Update_Time { get; set; }
}
