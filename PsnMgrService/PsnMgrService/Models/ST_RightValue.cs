using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_RightValue
{
    public string Right_ID { get; set; } = null!;

    public string Form_Name { get; set; } = null!;

    public string Right_Name { get; set; } = null!;

    public string? Remark { get; set; }
}
