using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_ExtendFunc
{
    public string Menu_No { get; set; } = null!;

    public string Menu_Name { get; set; } = null!;

    public bool? Is_Menu { get; set; }

    public bool Is_Use { get; set; }
}
