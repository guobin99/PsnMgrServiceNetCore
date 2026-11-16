using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_TakeFund
{
    public int ID_KEY { get; set; }

    public int Person_ID { get; set; }

    public DateTime Take_Date { get; set; }

    public decimal Take_Fund { get; set; }

    public decimal? Card_Balance { get; set; }

    public string? MOC_No { get; set; }

    public string? User_No { get; set; }

    public string? Remark { get; set; }
}
