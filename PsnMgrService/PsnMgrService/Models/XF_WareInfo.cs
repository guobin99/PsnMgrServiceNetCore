using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class XF_WareInfo
{
    public int Ware_ID { get; set; }

    public string System_No { get; set; } = null!;

    public string Ware_No { get; set; } = null!;

    public string Ware_Name { get; set; } = null!;

    public decimal Ware_Price { get; set; }

    public byte Ware_Discount { get; set; }

    public string? Ware_Unit { get; set; }

    public string? Remark { get; set; }

    public string? Ware_Type { get; set; }

    public string? Ware_PY { get; set; }

    public string? Ware_Bar_Code { get; set; }
}
