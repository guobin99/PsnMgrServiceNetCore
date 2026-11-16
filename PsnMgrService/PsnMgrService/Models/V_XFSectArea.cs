using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class V_XFSectArea
{
    public string Area_No { get; set; } = null!;

    public string Sect_No { get; set; } = null!;

    public string Sect_Name { get; set; } = null!;

    public int IsSect { get; set; }

    public string Term_No { get; set; } = null!;
}
