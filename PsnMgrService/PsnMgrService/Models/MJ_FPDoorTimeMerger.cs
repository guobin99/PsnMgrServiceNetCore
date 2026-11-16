using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_FPDoorTimeMerger
{
    public string Ctrl_No { get; set; } = null!;

    public int Door_No { get; set; }

    public int? TimeMerger_No1 { get; set; }

    public int? TimeMerger_No2 { get; set; }

    public int? TimeMerger_No3 { get; set; }

    public int? TimeMerger_No4 { get; set; }

    public int? TimeMerger_No5 { get; set; }

    public int? TimeMerger_No6 { get; set; }

    public int? TimeMerger_No7 { get; set; }

    public int? TimeMerger_No8 { get; set; }

    public int? TimeMerger_No9 { get; set; }

    public int? TimeMerger_No10 { get; set; }

    public string? Remark { get; set; }
}
