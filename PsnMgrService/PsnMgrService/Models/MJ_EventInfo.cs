using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_EventInfo
{
    public byte Event_Type { get; set; }

    public byte Event_Code { get; set; }

    public string Event_Name { get; set; } = null!;

    public byte? Record_Color { get; set; }

    public string? Info_Style { get; set; }

    public bool? Is_Up { get; set; }
}
