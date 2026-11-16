using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_PsnSpecDate
{
    public int ID_KEY { get; set; }

    public int? SpecTime_No { get; set; }

    public string Spec_Date { get; set; } = null!;
}
