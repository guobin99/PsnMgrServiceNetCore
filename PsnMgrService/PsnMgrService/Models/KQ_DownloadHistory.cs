using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class KQ_DownloadHistory
{
    public int ID_KEY { get; set; }

    public int Person_ID { get; set; }

    public string? Card_No { get; set; }

    public string Brush_Date { get; set; } = null!;

    public string Brush_Time { get; set; } = null!;

    public string Moc_No { get; set; } = null!;

    public short Data_Flag { get; set; }

    public DateTime? Brush_DateTime { get; set; }

    public bool? Is_Falsity { get; set; }

    public string? FileName { get; set; }
}
