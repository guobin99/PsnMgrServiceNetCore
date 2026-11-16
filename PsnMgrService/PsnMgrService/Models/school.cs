using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class school
{
    public int school_id { get; set; }

    public string? school_code { get; set; }

    public string school_name { get; set; } = null!;

    public string? address { get; set; }

    public string? contact_phone { get; set; }

    public string? remark { get; set; }

    public DateTime? created_at { get; set; }
}
