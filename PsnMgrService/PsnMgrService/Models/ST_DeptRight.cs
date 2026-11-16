using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_DeptRight
{
    public string User_No { get; set; } = null!;

    public int Dept_ID { get; set; }

    public byte Check_State { get; set; }

    public string? Remark { get; set; }
}
