using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class V_STPerson
{
    public string? dept_name { get; set; }

    public string? dept_no { get; set; }

    public int? dept_id { get; set; }

    public string card_no { get; set; } = null!;

    public int person_id { get; set; }

    public string person_name { get; set; } = null!;

    public string person_no { get; set; } = null!;

    public string? cert_name { get; set; }

    public string? cert_code { get; set; }

    public string? duty { get; set; }

    public string? title { get; set; }

    public string? nationality { get; set; }

    public string? sex { get; set; }

    public DateTime? birthday { get; set; }

    public string? is_marry { get; set; }

    public string? nativePlace { get; set; }

    public string? address { get; set; }

    public string? residence { get; set; }

    public bool is_del { get; set; }

    public string? remark { get; set; }
}
