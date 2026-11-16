using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_PersonLastInfo
{
    public int ID_KEY { get; set; }

    public int Person_ID { get; set; }

    public string? APB_No { get; set; }

    public string? HideBack_No { get; set; }

    public DateTime? LastIn_Time { get; set; }

    public string? LastIn_Ctrl { get; set; }

    public int? LastIn_Door { get; set; }

    public DateTime? LastOut_Time { get; set; }
}
