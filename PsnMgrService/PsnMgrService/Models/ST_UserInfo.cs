using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_UserInfo
{
    public string User_No { get; set; } = null!;

    public string User_Name { get; set; } = null!;

    public string? User_Pwd { get; set; }

    public byte? User_Type { get; set; }

    public string? Card_No { get; set; }

    public string? User_Moc { get; set; }

    public string? Role_No { get; set; }

    public bool? Is_Del { get; set; }

    public bool? Is_Using { get; set; }

    public DateTime? Login_Time { get; set; }

    public string? Computer_Name { get; set; }

    public string? Using_Model { get; set; }

    public string? Using_Form { get; set; }

    public string? Current_Menu { get; set; }

    public string? Remark { get; set; }
}
