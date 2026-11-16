using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class KQ_TransferTable
{
    public string Table_Name { get; set; } = null!;

    public string Date_Field { get; set; } = null!;

    public short Transfer_Index { get; set; }

    public short Delete_Index { get; set; }
}
