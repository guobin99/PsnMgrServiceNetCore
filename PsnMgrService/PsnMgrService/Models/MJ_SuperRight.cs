using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_SuperRight
{
    public int Super_No { get; set; }

    public byte? Super_Type { get; set; }

    public int? Person_ID { get; set; }

    public string? Card_No { get; set; }

    public string? Super_Pwd { get; set; }

    public string? Ctrl_No { get; set; }
}
