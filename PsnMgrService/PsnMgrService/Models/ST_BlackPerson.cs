using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_BlackPerson
{
    public int ID_KEY { get; set; }

    public int? Person_ID { get; set; }

    public string Card_No { get; set; } = null!;

    public DateTime? Del_Date { get; set; }

    public bool? Del_Sign { get; set; }

    public string? Remark { get; set; }
}
