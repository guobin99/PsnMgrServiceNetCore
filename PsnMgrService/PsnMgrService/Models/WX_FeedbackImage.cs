using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class WX_FeedbackImage
{
    public long ID_KEY { get; set; }

    public string Ticket_No { get; set; } = null!;

    public int Seq { get; set; }

    public string? Content_Type { get; set; }

    public byte[] Photo { get; set; } = null!;

    public DateTime Create_Time { get; set; }
}
