using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class XF_Section
{
    public string Sect_No { get; set; } = null!;

    public string Sect_Name { get; set; } = null!;

    public string? Remark { get; set; }

    public virtual ICollection<XF_Area> XF_Areas { get; set; } = new List<XF_Area>();
}
