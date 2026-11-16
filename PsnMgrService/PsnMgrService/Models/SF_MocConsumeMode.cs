using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class SF_MocConsumeMode
{
    public long ID_KEY { get; set; }

    public string? Moc_No { get; set; }

    public string? Repast_No { get; set; }

    public byte? Consume_Mode { get; set; }

    public string? Remark { get; set; }
}
