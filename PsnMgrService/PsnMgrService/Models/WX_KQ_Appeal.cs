using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class WX_KQ_Appeal
{
    public long ID_KEY { get; set; }

    public int Person_ID { get; set; }

    public string? Appeal_Date { get; set; }

    public string? Appeal_Type { get; set; }

    public string? Reason { get; set; }

    public string? Status { get; set; }

    public string? User_No { get; set; }

    public DateTime? Create_Time { get; set; }

    public DateTime? Update_Time { get; set; }
}
