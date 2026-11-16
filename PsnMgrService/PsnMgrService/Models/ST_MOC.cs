using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_MOC
{
    public string MOC_No { get; set; } = null!;

    public string MOC_Name { get; set; } = null!;

    public string System_No { get; set; } = null!;

    public string? Area_No { get; set; }

    public string? MOC_Type { get; set; }

    public string? IP_Addr { get; set; }

    public string? Addr485 { get; set; }

    public string? COM_No { get; set; }

    public byte? Consume_Mode { get; set; }

    public int? Key_Type { get; set; }

    public int? Discount_Kind { get; set; }

    public decimal? Limit_Fund { get; set; }

    public int? Limit_Deal { get; set; }

    public int? Unit_Number { get; set; }

    public byte? Unit { get; set; }

    public decimal? Unit_Price { get; set; }

    public bool Is_FirstInput { get; set; }

    public bool Is_Discount { get; set; }

    public bool Is_Correct { get; set; }

    public bool Is_UseSubsidy { get; set; }

    public bool? Is_OrderCtrl { get; set; }

    public byte? KQ_MocType { get; set; }

    public string? Comm_Content { get; set; }

    public byte? Comm_Status { get; set; }

    public byte? KQ_UseType { get; set; }

    public string? Telphone { get; set; }

    public string? Remark { get; set; }

    public byte? Download_Status { get; set; }

    public DateTime? Download_Time { get; set; }

    public bool? IsSel { get; set; }

    public bool Use_KQServer { get; set; }

    public DateTime? name_down_time { get; set; }

    public string? moc_model { get; set; }

    public string? Users_No { get; set; }

    public string? moc_sn { get; set; }
}
