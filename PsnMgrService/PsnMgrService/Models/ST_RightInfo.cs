using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_RightInfo
{
    public int Menu_ID { get; set; }

    public string Form_Name { get; set; } = null!;

    public string? Right_Values { get; set; }

    public string? Remark { get; set; }
}
