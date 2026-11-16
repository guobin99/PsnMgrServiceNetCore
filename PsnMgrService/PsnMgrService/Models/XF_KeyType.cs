using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class XF_KeyType
{
    public int Key_Type { get; set; }

    public int Key_Value { get; set; }

    public string Key_TypeName { get; set; } = null!;

    public decimal Key_Fund { get; set; }

    public string? Remark { get; set; }
}
