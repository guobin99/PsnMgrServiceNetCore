using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class st_moc_upload_name_log
{
    public long ID_KEY { get; set; }

    public int? person_id { get; set; }

    public string? moc_no { get; set; }

    public DateTime upload_time { get; set; }

    public int state { get; set; }

    public string? err_msg { get; set; }

    public string? remark { get; set; }
}
