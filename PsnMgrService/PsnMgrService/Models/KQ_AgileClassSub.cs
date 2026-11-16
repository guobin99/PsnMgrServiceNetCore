using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class KQ_AgileClassSub
{
    public int ID_KEY { get; set; }

    public string Class_No { get; set; } = null!;

    public string Shift_ClassNo { get; set; } = null!;

    public virtual KQ_AgileClass Class_NoNavigation { get; set; } = null!;
}
