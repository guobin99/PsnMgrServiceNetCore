using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_ResParam
{
    public byte Ctrl_Type { get; set; }

    public string? Ctrl_Name { get; set; }

    public byte Res_Type { get; set; }

    public byte? Idx_No { get; set; }

    public string? Res_Name { get; set; }

    public byte? Res_Number { get; set; }

    public bool? Is_Extend { get; set; }
}
