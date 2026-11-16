using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class KQ_InOutDataHistory
{
    public int ID_KEY { get; set; }

    public int? Person_ID { get; set; }

    public string? Card_No { get; set; }

    public string Brush_Date { get; set; } = null!;

    public string? Data_In { get; set; }

    public string? Data_Out { get; set; }
}
