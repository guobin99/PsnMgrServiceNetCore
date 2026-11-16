using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class WX_VisitorCard
{
    public string Card_No { get; set; } = null!;

    public int Mj_Person_ID { get; set; }

    public byte State { get; set; }

    public string? Apply_No { get; set; }

    public DateTime? Bind_Time { get; set; }

    public DateTime? Release_Time { get; set; }

    public string? Remark { get; set; }
}
