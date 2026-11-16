using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_RoleRight
{
    public int ID_KEY { get; set; }

    public string Role_No { get; set; } = null!;

    public int Menu_ID { get; set; }

    public string? Right_Values { get; set; }
}
