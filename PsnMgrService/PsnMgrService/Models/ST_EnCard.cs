using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_EnCard
{
    public int ID_KEY { get; set; }

    public string Card_No { get; set; } = null!;

    public string? Remark { get; set; }
}
