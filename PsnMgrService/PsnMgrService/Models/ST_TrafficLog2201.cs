using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_TrafficLog2201
{
    public int ID_Key { get; set; }

    public long id { get; set; }

    public string Person_No { get; set; } = null!;

    public string? EventType { get; set; }

    public string? Photo { get; set; }

    public string? PhotoPath { get; set; }

    public int? Is_Async { get; set; }

    public string? Async_Date { get; set; }

    public string? Ctrl_No { get; set; }

    public int? Door_No { get; set; }

    public int? ResultCode { get; set; }

    public DateTime? Create_Date { get; set; }

    public string? Remark { get; set; }

    public string? Ctrl_Name { get; set; }

    public string? Door_Name { get; set; }
}
