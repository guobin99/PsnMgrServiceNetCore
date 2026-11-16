using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class WX_SmsCode
{
    public long ID_KEY { get; set; }

    public string? Mobile { get; set; }

    public string? Code { get; set; }

    public string? Scene { get; set; }

    public DateTime? Expire_Time { get; set; }

    public bool? Used { get; set; }

    public DateTime? Create_Time { get; set; }
}
