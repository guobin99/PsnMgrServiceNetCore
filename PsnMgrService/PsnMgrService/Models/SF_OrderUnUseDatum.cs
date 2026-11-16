using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class SF_OrderUnUseDatum
{
    public int Id_key { get; set; }

    public int? Person_Id { get; set; }

    public string? Order_Date { get; set; }

    public string? Repast_No { get; set; }

    public string? Order_No { get; set; }
}
