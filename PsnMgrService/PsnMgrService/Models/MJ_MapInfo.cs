using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_MapInfo
{
    public string Map_No { get; set; } = null!;

    public string Map_Name { get; set; } = null!;

    public byte[]? Map_Image { get; set; }

    public int? Door_TotalNum { get; set; }
}
