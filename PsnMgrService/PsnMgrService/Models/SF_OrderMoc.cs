using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class SF_OrderMoc
{
    public string? Area_No { get; set; }

    public string Moc_No { get; set; } = null!;

    public string? Moc_Addr { get; set; }

    public string? Note { get; set; }
}
