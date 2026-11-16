using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class XF_Area
{
    public string Area_No { get; set; } = null!;

    public string Sect_No { get; set; } = null!;

    public string Area_Name { get; set; } = null!;

    public string? Remark { get; set; }

    public virtual XF_Section Sect_NoNavigation { get; set; } = null!;
}
