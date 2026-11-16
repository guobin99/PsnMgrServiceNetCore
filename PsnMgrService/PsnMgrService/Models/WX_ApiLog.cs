using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class WX_ApiLog
{
    public long ID_KEY { get; set; }

    public int? Person_ID { get; set; }

    public string? Api_Url { get; set; }

    public string? Http_Method { get; set; }

    public string? Request_Body { get; set; }

    public string? Response_Body { get; set; }

    public string? IP { get; set; }

    public int? Elapsed { get; set; }

    public bool? Success { get; set; }

    public DateTime? Create_Time { get; set; }
}
