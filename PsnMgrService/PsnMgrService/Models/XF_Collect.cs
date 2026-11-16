using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class XF_Collect
{
    public int ID_KEY { get; set; }

    public string? IP_Addr { get; set; }

    public int? Com_Port { get; set; }

    public byte? Chg_Status { get; set; }
}
