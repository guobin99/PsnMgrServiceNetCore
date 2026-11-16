using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_SubsidyAccount
{
    public int Person_ID { get; set; }

    public decimal Subsidy_Fund { get; set; }

    public decimal Consume_Fund { get; set; }

    public DateTime Subsidy_Date { get; set; }

    public long ID_KEY { get; set; }
}
