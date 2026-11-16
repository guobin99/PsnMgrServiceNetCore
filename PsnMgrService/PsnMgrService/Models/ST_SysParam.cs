using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_SysParam
{
    public int Param_ID { get; set; }

    public string Param_Code { get; set; } = null!;

    public string Param_Name { get; set; } = null!;

    public string Param_Value { get; set; } = null!;
}
