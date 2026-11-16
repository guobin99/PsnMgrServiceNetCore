using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_Person
{
    public int Person_ID { get; set; }

    public int? Dept_ID { get; set; }

    public string Card_No { get; set; } = null!;

    public string Person_No { get; set; } = null!;

    public string Person_Name { get; set; } = null!;

    public string? Person_Type { get; set; }

    public string? Cert_Name { get; set; }

    public string? Cert_Code { get; set; }

    public string? Duty { get; set; }

    public string? Title { get; set; }

    public string? Nationality { get; set; }

    public string? Sex { get; set; }

    public DateTime? Birthday { get; set; }

    public bool? Is_Marry { get; set; }

    public string? NativePlace { get; set; }

    public string? Address { get; set; }

    public string? Residence { get; set; }

    public string? OnWork_Date { get; set; }

    public string? OffWork_Date { get; set; }

    public bool Is_Del { get; set; }

    public string? Remark { get; set; }

    public string? Card_SN { get; set; }

    public string? C_PSD { get; set; }

    public DateTime? update_time { get; set; }

    public DateTime? wx_syn_time { get; set; }

    public string? mobile { get; set; }

    public string? id_number { get; set; }

    public string? OpenID { get; set; }

    public string? UnionID { get; set; }

    public string? Reserved1 { get; set; }

    public string? Reserved2 { get; set; }

    public string? Reserved3 { get; set; }

    public string? Reserved4 { get; set; }

    public string? Reserved5 { get; set; }
}
