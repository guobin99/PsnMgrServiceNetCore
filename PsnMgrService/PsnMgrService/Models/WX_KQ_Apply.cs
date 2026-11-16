using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class WX_KQ_Apply
{
    public long ID_KEY { get; set; }

    public string Apply_No { get; set; } = null!;

    public int Person_ID { get; set; }

    public string? Person_No { get; set; }

    public string? Person_Name { get; set; }

    public string Apply_Type { get; set; } = null!;

    public string? Supply_Date { get; set; }

    public string? Supply_Time { get; set; }

    public short? Supply_Class { get; set; }

    public string? Kind_No { get; set; }

    public string? Kind_Name { get; set; }

    public string? Leave_Bgn { get; set; }

    public string? Leave_End { get; set; }

    public double? Leave_Hour { get; set; }

    public int? Leave_Days { get; set; }

    public string? Appeal_Date { get; set; }

    public string? Appeal_Type { get; set; }

    public string? Appeal_Time { get; set; }

    public string? Reason { get; set; }

    public byte Status { get; set; }

    public string? Audit_User { get; set; }

    public DateTime? Audit_Time { get; set; }

    public string? Audit_Remark { get; set; }

    public string? Target_Table { get; set; }

    public string? Target_Ids { get; set; }

    public string? Source { get; set; }

    public DateTime Create_Time { get; set; }
}
