using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class XF_WareInfoPhoto
{
    public int Ware_ID { get; set; }

    public byte[]? Photo { get; set; }
}
