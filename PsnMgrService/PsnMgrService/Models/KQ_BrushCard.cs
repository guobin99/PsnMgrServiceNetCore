using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class KQ_BrushCard
{
    public int ID_KEY { get; set; }

    public int Person_ID { get; set; }

    public string? Card_No { get; set; }

    public string? Brush_Date { get; set; }

    public string? Brush_Data { get; set; }
}
