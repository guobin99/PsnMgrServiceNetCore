using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class VisitorApply
{
    public int ID_KEY { get; set; }

    public string Apply_No { get; set; } = null!;

    public int? Person_ID { get; set; }

    public string? Person_Mobile { get; set; }

    public string? OpenID { get; set; }

    public string? Visitor_Number { get; set; }

    public DateTime? Start_Date { get; set; }

    public DateTime? End_Date { get; set; }

    public int? VisitOrNot { get; set; }

    public string? Subject_Matter { get; set; }

    public string? Carry_items { get; set; }

    public DateTime? CreateTime { get; set; }

    public bool? Is_Up { get; set; }

    public string? VisitorID_number { get; set; }

    public string? car_number { get; set; }

    public string? Visitor_Company { get; set; }

    public string? visitor_type { get; set; }

    public string? Empower_Card_No { get; set; }

    public string? Approved_User { get; set; }

    public DateTime? Approved_Time { get; set; }

    public string? Approved_Reason { get; set; }

    public DateTime? check_intime { get; set; }

    public DateTime? check_outtime { get; set; }

    public DateTime update_time { get; set; }

    public DateTime? syn_time { get; set; }

    public int visitors_number { get; set; }
}
