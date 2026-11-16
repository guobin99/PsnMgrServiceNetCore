using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_Identity
{
    public string Table_Name { get; set; } = null!;

    public int Current_ID { get; set; }
}
