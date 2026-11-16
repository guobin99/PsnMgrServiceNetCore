using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_DVSVideoInfo
{
    public int ID_KEY { get; set; }

    public string? Card_No { get; set; }

    public string? Brush_Time { get; set; }

    public string? Brush_Date { get; set; }

    public string? File_Name { get; set; }

    public string? File_Path { get; set; }

    public string? Remark { get; set; }
}
