using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class KQ_Account
{
    public int ID_KEY { get; set; }

    public string? Account_Code { get; set; }

    public string Account_Name { get; set; } = null!;

    public DateTime? Account_Date { get; set; }

    public string? Date_Bgn { get; set; }

    public string? Date_End { get; set; }

    public byte Account_Flag { get; set; }

    public string? Remark { get; set; }

    public string? User_No { get; set; }

    public DateTime? Update_Date { get; set; }
}
