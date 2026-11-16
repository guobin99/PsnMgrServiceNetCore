using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class KQ_Class
{
    public string Class_No { get; set; } = null!;

    public string Class_Name { get; set; } = null!;

    public short? Sect1_Ahead { get; set; }

    public string? Sect1_InTime { get; set; }

    public byte? Sect1_InCard { get; set; }

    public string? Sect1_OutTime { get; set; }

    public byte? Sect1_OutCard { get; set; }

    public short? Sect1_After { get; set; }

    public short? Sect1_Deduct { get; set; }

    public bool Sect1_Overtime { get; set; }

    public short? Sect2_Ahead { get; set; }

    public string? Sect2_InTime { get; set; }

    public byte? Sect2_InCard { get; set; }

    public string? Sect2_OutTime { get; set; }

    public byte? Sect2_OutCard { get; set; }

    public short? Sect2_After { get; set; }

    public short? Sect2_Deduct { get; set; }

    public bool Sect2_Overtime { get; set; }

    public short? Sect3_Ahead { get; set; }

    public string? Sect3_InTime { get; set; }

    public byte? Sect3_InCard { get; set; }

    public string? Sect3_OutTime { get; set; }

    public byte? Sect3_OutCard { get; set; }

    public short? Sect3_After { get; set; }

    public short? Sect3_Deduct { get; set; }

    public bool Sect3_Overtime { get; set; }

    public short? Sect4_Ahead { get; set; }

    public string? Sect4_InTime { get; set; }

    public byte? Sect4_InCard { get; set; }

    public string? Sect4_OutTime { get; set; }

    public byte? Sect4_OutCard { get; set; }

    public short? Sect4_Deduct { get; set; }

    public short? Sect4_After { get; set; }

    public bool Sect4_Overtime { get; set; }

    public short? Sect5_Ahead { get; set; }

    public string? Sect5_InTime { get; set; }

    public byte? Sect5_InCard { get; set; }

    public string? Sect5_OutTime { get; set; }

    public byte? Sect5_OutCard { get; set; }

    public short? Sect5_Deduct { get; set; }

    public short? Sect5_After { get; set; }

    public bool Sect5_Overtime { get; set; }

    public byte? Class_Mark { get; set; }

    public string? Remark { get; set; }

    public string? User_No { get; set; }

    public DateTime? Update_Date { get; set; }
}
