using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_Fingerprint
{
    public long ID_KEY { get; set; }

    public int Person_Id { get; set; }

    public string? Person_No { get; set; }

    public string? Person_Name { get; set; }

    public byte User_type { get; set; }

    public byte FP_Type { get; set; }

    public byte[]? FP_Data { get; set; }

    public string? Remark { get; set; }

    public long? FP_Index { get; set; }

    public DateTime Update_Time { get; set; }

    public bool IS_Del { get; set; }
}
