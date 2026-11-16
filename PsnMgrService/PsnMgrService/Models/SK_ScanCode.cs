using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class SK_ScanCode
{
    public long ID_Key { get; set; }

    public string? Moc_No { get; set; }

    public DateTime? Get_Time { get; set; }

    public int? Person_ID { get; set; }

    public string? Person_No { get; set; }

    public decimal? QR_Fund { get; set; }

    public decimal? QR_Fund_Free { get; set; }

    public int State { get; set; }

    public DateTime? moc_read_time { get; set; }

    public string? Remark { get; set; }

    public long? soruce_id_key { get; set; }

    public double? Consume_TimeLen { get; set; }

    public string? open_id { get; set; }

    public int pay_type { get; set; }

    public int? pay_state { get; set; }

    public int? Consume_Mode { get; set; }

    public decimal? Unit_Price { get; set; }

    public int? Unit { get; set; }

    public int? Unit_Number { get; set; }

    public string? QR_Code { get; set; }
}
