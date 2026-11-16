using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_Dictionary
{
    public int ID_KEY { get; set; }

    public string Table_Name { get; set; } = null!;

    public string? Table_ChsName { get; set; }

    public string? Dict_Id { get; set; }

    public string? Dict_Value { get; set; }

    public bool? Is_SysCol { get; set; }

    public string? Remark { get; set; }
}
