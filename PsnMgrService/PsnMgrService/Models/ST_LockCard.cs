using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_LockCard
{
    public int ID_KEY { get; set; }

    public int Person_ID { get; set; }

    public DateTime Lock_Date { get; set; }

    public string User_No { get; set; } = null!;

    public string? Remark { get; set; }
}
