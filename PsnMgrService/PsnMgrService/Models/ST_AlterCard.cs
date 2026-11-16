using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_AlterCard
{
    public int ID_KEY { get; set; }

    public int Person_ID { get; set; }

    public DateTime Alter_Date { get; set; }

    public string Card_No { get; set; } = null!;

    public string? NewCard_No { get; set; }

    public decimal? Alter_Fee { get; set; }

    public byte Alter_Type { get; set; }

    public string User_No { get; set; } = null!;

    public string? Remark { get; set; }
}
