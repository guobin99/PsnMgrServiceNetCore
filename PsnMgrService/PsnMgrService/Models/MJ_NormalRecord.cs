using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_NormalRecord
{
    public int ID_KEY { get; set; }

    public DateTime BrushCard_Time { get; set; }

    public int Person_ID { get; set; }

    public string Ctrl_No { get; set; } = null!;

    public int? Door_No { get; set; }

    public string? Door_Name { get; set; }

    public string? Card_No { get; set; }

    public int? Event_Code { get; set; }

    public bool Is_InDoor { get; set; }

    public int? bReaded { get; set; }

    public double? Temperature { get; set; }
}
