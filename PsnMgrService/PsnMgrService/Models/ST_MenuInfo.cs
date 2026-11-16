using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_MenuInfo
{
    public int Menu_ID { get; set; }

    public string System_No { get; set; } = null!;

    public string Menu_No { get; set; } = null!;

    public string Menu_Name { get; set; } = null!;

    public string? Menu_Hint { get; set; }

    public int? Para_ID { get; set; }

    public byte? Call_Mode { get; set; }

    public string? Form_Name { get; set; }

    public string? ShortKey { get; set; }

    public byte? Menu_Index { get; set; }

    public short? Image_Index { get; set; }

    public byte? Separator { get; set; }

    public bool? InToolBar { get; set; }

    public string? Remark { get; set; }
}
