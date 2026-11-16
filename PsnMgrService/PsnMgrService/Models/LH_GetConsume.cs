using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class LH_GetConsume
{
    public int ID_Key { get; set; }

    public string? Card_No { get; set; }

    public int Moc_No { get; set; }

    public DateTime Get_Time { get; set; }

    public double? Consume_AllTime { get; set; }

    public int? Consume_AllCopy { get; set; }

    public decimal Consume_AllFund { get; set; }

    public double? Consume_AllCapacity { get; set; }

    public string? User_Name { get; set; }

    public string? Remark { get; set; }
}
