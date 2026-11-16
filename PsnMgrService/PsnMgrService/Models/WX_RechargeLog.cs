using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class WX_RechargeLog
{
    public long ID_KEY { get; set; }

    public int? Person_ID { get; set; }

    public string Out_Trade_No { get; set; } = null!;

    public string? Transaction_Id { get; set; }

    public decimal? Total_Fee { get; set; }

    public string? Status { get; set; }

    public string? Err_Code { get; set; }

    public string? Err_Msg { get; set; }

    public string? Trade_Type { get; set; }

    public string? Channel { get; set; }

    public string? Raw_Send { get; set; }

    public string? Raw_Recv { get; set; }

    public DateTime? Create_Time { get; set; }

    public DateTime? Update_Time { get; set; }
}
