using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class student_parent
{
    public int bind_id { get; set; }

    public int person_id { get; set; }

    public int? parent_id { get; set; }

    public string? parent_openid { get; set; }

    public bool? is_primary { get; set; }

    public DateTime? created_at { get; set; }

    public string? relation_type { get; set; }

    public string? remark { get; set; }

    public string? contract_id { get; set; }

    public string? out_contract_code { get; set; }

    public DateTime? contract_time { get; set; }

    public string? contract_status { get; set; }

    public bool is_del { get; set; }

    public int? school_id { get; set; }

    public int? grade_id { get; set; }

    public string? _class { get; set; }

    public DateTime updated_at { get; set; }
}
