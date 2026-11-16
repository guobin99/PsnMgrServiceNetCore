using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_Door
{
    public string Ctrl_No { get; set; } = null!;

    public int Door_No { get; set; }

    public string Door_Name { get; set; } = null!;

    public byte Door_Type { get; set; }

    public byte DoorMagic_State { get; set; }

    public byte DoorBtn_State { get; set; }

    public byte DoorLock_State { get; set; }

    public byte DoorLock_TimeLen { get; set; }

    public byte DoorClose_TimeLen { get; set; }

    public byte OpenAlarm_TimeLen { get; set; }

    public byte CardReader_Type { get; set; }

    public byte ReadCard_Mode { get; set; }

    public byte Card_Number { get; set; }

    public string Menace_Pwd { get; set; } = null!;

    public bool Is_APB { get; set; }

    public bool Is_HiddenBack { get; set; }

    public bool Is_PwdKeyBoard { get; set; }

    public int Allow_HoldNumber { get; set; }

    public bool Is_ReadCardAttend { get; set; }

    public int? DoorTime_No { get; set; }

    public byte LockWithDoor { get; set; }

    public int? Which_Door { get; set; }

    public int? MeetingMode { get; set; }

    public string? IP { get; set; }

    public string? HideBack_No { get; set; }

    public string? APB_No { get; set; }

    public byte DVSMode { get; set; }

    public string? DVSServerIP { get; set; }

    public int? tipsdays { get; set; }

    public string? tips { get; set; }

    public string? effSDate { get; set; }

    public string? effEDate { get; set; }

    public string? idtypepass { get; set; }

    public string? passids { get; set; }

    public bool? Is_Up { get; set; }

    public bool? IsVis { get; set; }
}
