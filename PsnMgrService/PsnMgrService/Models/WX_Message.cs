using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class WX_Message
{
    public long ID_KEY { get; set; }

    public int Person_ID { get; set; }

    public string? Msg_Type { get; set; }

    public string? Title { get; set; }

    public string? Content { get; set; }

    public bool? Is_Read { get; set; }

    public DateTime? Create_Time { get; set; }
}
