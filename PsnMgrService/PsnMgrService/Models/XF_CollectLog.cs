using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class XF_CollectLog
{
    public int Id_Key { get; set; }

    public DateTime? Log_DateTime { get; set; }

    public string? Log_Msg { get; set; }
}
