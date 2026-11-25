using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PsnMgrService.Models;

/// <summary>ST_UserInfo 的 BS 扩展列（数据库新增可空列，不影响 CS 端）。</summary>
public partial class ST_UserInfo
{
    public int? BS_Dept_ID { get; set; }

    public short? BS_Fail_Count { get; set; }

    public DateTime? BS_Lock_Until { get; set; }

    public DateTime? BS_Pwd_Update_Time { get; set; }

    public string? BS_Last_Login_IP { get; set; }
}

/// <summary>ST_Department 的 BS 扩展列。层级仍以 Dept_No 为权威来源。</summary>
public partial class ST_Department
{
    public int? BS_Parent_ID { get; set; }

    public byte? BS_Level { get; set; }

    public int? BS_Sort_No { get; set; }

    public string? BS_Dept_Type { get; set; }
}

/// <summary>菜单权限位到前端权限码与接口路由的映射。</summary>
[Table("BS_RightCodeMap")]
public class BS_RightCodeMap
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ID_KEY { get; set; }

    public int Menu_ID { get; set; }

    [Required]
    [Column(TypeName = "char(1)")]
    public string Right_ID { get; set; } = "0";

    [Required]
    [StringLength(64)]
    public string Permission_Code { get; set; } = null!;

    [StringLength(200)]
    public string? Api_Route { get; set; }

    [StringLength(200)]
    public string? Remark { get; set; }
}

/// <summary>操作员部门数据范围模式：1 全部、2 本部门及下级、3 指定部门。</summary>
[Table("BS_UserDataScope")]
public class BS_UserDataScope
{
    [Key]
    [StringLength(20)]
    public string User_No { get; set; } = null!;

    public byte Scope_Type { get; set; }

    public int? Owner_Dept_ID { get; set; }

    [StringLength(200)]
    public string? Summary { get; set; }

    public DateTime Update_Time { get; set; }

    [StringLength(20)]
    public string? Update_User { get; set; }
}

/// <summary>操作员设备（机台）权限。</summary>
[Table("BS_UserMOCRight")]
[PrimaryKey(nameof(User_No), nameof(MOC_No))]
public class BS_UserMOCRight
{
    [StringLength(20)]
    public string User_No { get; set; } = null!;

    [StringLength(6)]
    public string MOC_No { get; set; } = null!;

    public byte Right_Level { get; set; }

    public DateTime Update_Time { get; set; }
}

/// <summary>操作员子系统权限。人员级 ST_SubSysRight 保持只读。</summary>
[Table("BS_UserSubSysRight")]
[PrimaryKey(nameof(User_No), nameof(System_No))]
public class BS_UserSubSysRight
{
    [StringLength(20)]
    public string User_No { get; set; } = null!;

    [StringLength(10)]
    public string System_No { get; set; } = null!;

    public bool Is_Allow { get; set; }

    public DateTime Update_Time { get; set; }
}

/// <summary>BS 操作审计日志（只追加，不提供修改删除接口）。</summary>
[Table("BS_OperLog")]
public class BS_OperLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long ID_KEY { get; set; }

    [StringLength(20)]
    public string? User_No { get; set; }

    [StringLength(20)]
    public string? User_Name { get; set; }

    [Required]
    [StringLength(30)]
    public string Module { get; set; } = null!;

    [Required]
    [StringLength(30)]
    public string Action { get; set; } = null!;

    [StringLength(100)]
    public string? Target { get; set; }

    [StringLength(50)]
    public string? Target_Key { get; set; }

    public string? Before_Value { get; set; }

    public string? After_Value { get; set; }

    [StringLength(45)]
    public string? IP { get; set; }

    [StringLength(200)]
    public string? User_Agent { get; set; }

    public byte Result { get; set; }

    [StringLength(500)]
    public string? Message { get; set; }

    [StringLength(36)]
    public string? Trace_ID { get; set; }

    public DateTime Opt_Time { get; set; }
}

/// <summary>BS 登录审计日志。</summary>
[Table("BS_LoginLog")]
public class BS_LoginLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long ID_KEY { get; set; }

    [StringLength(20)]
    public string? User_No { get; set; }

    [Required]
    [StringLength(20)]
    public string Login_Type { get; set; } = "password";

    public byte Result { get; set; }

    [StringLength(100)]
    public string? Fail_Reason { get; set; }

    [StringLength(45)]
    public string? IP { get; set; }

    [StringLength(200)]
    public string? User_Agent { get; set; }

    public DateTime Login_Time { get; set; }

    public DateTime? Logout_Time { get; set; }

    [StringLength(36)]
    public string? Session_ID { get; set; }
}

/// <summary>BS 运行配置与权限版本。</summary>
[Table("BS_SysConfig")]
public class BS_SysConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Config_ID { get; set; }

    [Required]
    [StringLength(64)]
    public string Config_Key { get; set; } = null!;

    [StringLength(500)]
    public string? Config_Value { get; set; }

    [StringLength(200)]
    public string? Remark { get; set; }

    public DateTime Update_Time { get; set; }
}

/// <summary>密码历史（只存 DES 密文，最近三次）。</summary>
[Table("BS_PwdHistory")]
public class BS_PwdHistory
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long ID_KEY { get; set; }

    [Required]
    [StringLength(20)]
    public string User_No { get; set; } = null!;

    [Required]
    [StringLength(200)]
    public string Pwd_Cipher { get; set; } = null!;

    public DateTime Create_Time { get; set; }
}

/// <summary>服务端令牌登记，用于重置密码、注销等场景吊销会话。</summary>
[Table("BS_AuthToken")]
public class BS_AuthToken
{
    [Key]
    [StringLength(36)]
    public string Token_ID { get; set; } = null!;

    [Required]
    [StringLength(20)]
    public string User_No { get; set; } = null!;

    [Required]
    [StringLength(128)]
    public string Token_Hash { get; set; } = null!;

    public DateTime Expire_Time { get; set; }

    public bool Revoked { get; set; }

    [StringLength(200)]
    public string? Client_Info { get; set; }

    public DateTime Create_Time { get; set; }
}

/// <summary>AppDbContext 的 BS_ 对象注册。原 CS 实体映射保持不动。</summary>
public partial class AppDbContext
{
    public virtual DbSet<BS_RightCodeMap> BS_RightCodeMaps { get; set; } = null!;
    public virtual DbSet<BS_UserDataScope> BS_UserDataScopes { get; set; } = null!;
    public virtual DbSet<BS_UserMOCRight> BS_UserMOCRights { get; set; } = null!;
    public virtual DbSet<BS_UserSubSysRight> BS_UserSubSysRights { get; set; } = null!;
    public virtual DbSet<BS_OperLog> BS_OperLogs { get; set; } = null!;
    public virtual DbSet<BS_LoginLog> BS_LoginLogs { get; set; } = null!;
    public virtual DbSet<BS_SysConfig> BS_SysConfigs { get; set; } = null!;
    public virtual DbSet<BS_PwdHistory> BS_PwdHistories { get; set; } = null!;
    public virtual DbSet<BS_AuthToken> BS_AuthTokens { get; set; } = null!;
}
