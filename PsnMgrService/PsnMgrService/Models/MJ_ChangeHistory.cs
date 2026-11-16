using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_ChangeHistory
{
    public int ID_Key { get; set; }

    public int? Person_ID { get; set; }

    public byte? Change_State { get; set; }

    public string? Change_Value { get; set; }

    public string? Change_Ctrl { get; set; }

    public DateTime? Change_Date { get; set; }
}
