using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_vi_ChangeHistory
{
    public string? Card_No { get; set; }

    public int? Door_No { get; set; }

    public int? PsnTime_No { get; set; }

    public string? Door_Pwd { get; set; }

    public DateTime? Card_NewDate { get; set; }

    public DateTime? Card_ValidDate { get; set; }

    public string? Change_Value { get; set; }

    public byte? Change_State { get; set; }

    public DateTime? Change_Date { get; set; }

    public string? Change_Ctrl { get; set; }

    public string? Ctrl_No { get; set; }

    public bool? FirstCardOpen { get; set; }
}
