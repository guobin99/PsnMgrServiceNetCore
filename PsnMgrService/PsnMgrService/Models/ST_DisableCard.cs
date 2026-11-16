using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_DisableCard
{
    public int ID_KEY { get; set; }

    public string Card_No { get; set; } = null!;

    public DateTime? Disable_Date { get; set; }

    public string? Disable_Cause { get; set; }

    public string? Remark { get; set; }
}
