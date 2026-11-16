using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_RoleInfo
{
    public string Role_No { get; set; } = null!;

    public string Role_Name { get; set; } = null!;

    public string? Remark { get; set; }
}
