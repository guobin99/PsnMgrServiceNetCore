using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_MeetingPerson
{
    public int ID_Key { get; set; }

    public string Ctrl_No { get; set; } = null!;

    public string? Door_No { get; set; }

    public int? Person_ID { get; set; }
}
