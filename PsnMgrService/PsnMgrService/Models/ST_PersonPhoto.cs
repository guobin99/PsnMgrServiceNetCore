using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class ST_PersonPhoto
{
    public int Person_ID { get; set; }

    public byte[]? Photo { get; set; }

    public DateTime? update_time { get; set; }

    public byte[]? id_photo { get; set; }

    public byte[]? img_qr_code { get; set; }
}
