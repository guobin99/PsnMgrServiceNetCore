using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_Controller
{
    public string Ctrl_No { get; set; } = null!;

    public string Ctrl_Name { get; set; } = null!;

    public string Ctrl_Type { get; set; } = null!;

    public string? IP_Addr { get; set; }

    public string? Com_Addr { get; set; }

    public bool Is_Extend { get; set; }

    public bool? Is_Online { get; set; }

    public byte? InterLock_Info { get; set; }

    public DateTime LastUpload { get; set; }

    public string? Users_No { get; set; }

    public bool Is_Video { get; set; }

    public string? Video_IP { get; set; }

    public string? Manager_Pwd { get; set; }

    public int? Ctrl_LargeType { get; set; }

    public string? Serial_Num { get; set; }

    public string? License_Code { get; set; }

    public int? Comm_Status { get; set; }

    public string? Comm_Content { get; set; }

    public DateTime? Download_Time { get; set; }

    public int? download_status { get; set; }

    public int IS_KQ { get; set; }

    public string Ctrl_WGType { get; set; } = null!;

    public bool? IsLicense { get; set; }

    public DateTime? LastOnLineTime { get; set; }

    public bool? Is_Up { get; set; }
}
