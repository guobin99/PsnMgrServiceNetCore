using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class WX_Feedback
{
    public long ID_KEY { get; set; }

    public string Ticket_No { get; set; } = null!;

    public int Person_ID { get; set; }

    public string? Person_No { get; set; }

    public string? Person_Name { get; set; }

    public string? Card_No { get; set; }

    public string Feed_Type { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string Contact { get; set; } = null!;

    public int Image_Count { get; set; }

    public byte Status { get; set; }

    public string? Reply_Content { get; set; }

    public string? Reply_User { get; set; }

    public DateTime? Reply_Time { get; set; }

    public string? Source { get; set; }

    public DateTime Create_Time { get; set; }
}
