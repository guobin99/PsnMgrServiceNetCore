using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class grade
{
    public int grade_id { get; set; }

    public int? school_id { get; set; }

    public string grade_code { get; set; } = null!;

    public string grade_name { get; set; } = null!;

    public int? grade_order { get; set; }

    public string? remark { get; set; }
}
