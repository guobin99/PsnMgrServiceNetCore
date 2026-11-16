using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_ClearTable
{
    public string Table_Name { get; set; } = null!;

    public string Date_Field { get; set; } = null!;

    public short Clear_Index { get; set; }

    public short Delete_Index { get; set; }

    public string? Other_Field { get; set; }
}
