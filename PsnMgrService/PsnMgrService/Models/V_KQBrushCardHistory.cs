using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class V_KQBrushCardHistory
{
    public int Person_ID { get; set; }

    public string? person_no { get; set; }

    public string? person_name { get; set; }

    public string? dept_no { get; set; }

    public string? dept_name { get; set; }

    public string? Card_No { get; set; }

    public string? Brush_Date { get; set; }

    public string? Brush_Data { get; set; }

    public int ID_KEY { get; set; }
}
