using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_SubSystem
{
    public string System_No { get; set; } = null!;

    public string System_Name { get; set; } = null!;

    public string? System_Title { get; set; }

    public string? System_ExeName { get; set; }

    public int? Index_No { get; set; }

    public DateTime? DBCreateDate { get; set; }

    public string? DBVersion { get; set; }

    public string? DBUpdateinfo { get; set; }

    public bool? IsCreateTable { get; set; }

    public bool? IsInitTable { get; set; }

    public string? Remark { get; set; }

    public byte? Update_State1 { get; set; }

    public byte? Update_State2 { get; set; }

    public byte? Update_State3 { get; set; }
}
