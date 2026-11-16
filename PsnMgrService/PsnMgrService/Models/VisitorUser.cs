using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class VisitorUser
{
    public int ID_KEY { get; set; }

    public string? VisitorID_number { get; set; }

    public string? ID_Type { get; set; }

    public string? OpenID { get; set; }

    public string? Visitor_Name { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Visitor_Company { get; set; }

    public string? VisitorPhotoUrl { get; set; }

    public DateTime? CreateTime { get; set; }

    public DateTime update_time { get; set; }

    public DateTime? syn_time { get; set; }

    public bool? Is_Up { get; set; }

    public byte[]? VisitorPhoto { get; set; }
}
