using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_Person
{
    public int Person_ID { get; set; }

    public string? Door_Pwd { get; set; }

    public byte? Person_State { get; set; }

    public string? User_No { get; set; }

    public DateTime? Update_Date { get; set; }

    public string? Acs_Card { get; set; }

    public DateTime? LastIn_Time { get; set; }

    public string? LastIn_Ctrl { get; set; }

    public int? LastIn_Door { get; set; }

    public DateTime? LastOut_Time { get; set; }

    public string? LastOut_Ctrl { get; set; }

    public int? LastOut_Door { get; set; }
}
