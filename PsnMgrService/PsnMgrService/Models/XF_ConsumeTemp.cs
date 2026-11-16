using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class XF_ConsumeTemp
{
    public long ID_KEY { get; set; }

    public int Person_ID { get; set; }

    public string System_No { get; set; } = null!;

    public int Consume_Mode { get; set; }

    public DateTime Consume_Time { get; set; }

    public decimal Consume_Fund { get; set; }

    public decimal Before_Fund { get; set; }

    public int? Consume_Copy { get; set; }

    public double? Consume_TimeLen { get; set; }

    public double? Consume_Capacity { get; set; }

    public string? Repast_No { get; set; }

    public int? Ware_ID { get; set; }

    public string MOC_No { get; set; } = null!;

    public string? COM_No { get; set; }

    public decimal Card_Balance { get; set; }

    public decimal? DB_Balance { get; set; }

    public decimal? Card_SmallAccount { get; set; }

    public byte Data_Type { get; set; }

    public bool? Is_Commited { get; set; }

    public long? Source_ID { get; set; }

    public DateTime? Download_Time { get; set; }

    public string? OrderID { get; set; }

    public string? BarCode { get; set; }
}
