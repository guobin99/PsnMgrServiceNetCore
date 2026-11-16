using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class SF_OrderArea
{
    public string Area_No { get; set; } = null!;

    public string? Area_Name { get; set; }

    public string? Note { get; set; }
}
