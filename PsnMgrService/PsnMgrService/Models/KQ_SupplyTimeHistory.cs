using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class KQ_SupplyTimeHistory
{
    public int ID_KEY { get; set; }

    public int Person_ID { get; set; }

    public string? Supply_Date { get; set; }

    public string Supply_Time { get; set; } = null!;

    public string? Supply_Reason { get; set; }

    public short Supply_Class { get; set; }

    public string? Remark { get; set; }

    public string? User_No { get; set; }

    public DateTime? Update_Date { get; set; }
}
