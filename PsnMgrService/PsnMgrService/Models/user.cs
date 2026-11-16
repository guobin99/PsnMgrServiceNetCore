using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class user
{
    public int id { get; set; }

    public string? username { get; set; }

    public string? password { get; set; }

    public string? email { get; set; }

    public string? mobile { get; set; }

    public string? openid { get; set; }

    public string? unionid { get; set; }

    public byte? age { get; set; }

    public byte? gender { get; set; }

    public string? hobbies { get; set; }

    public string? avatar { get; set; }

    public DateTime created_at { get; set; }

    public DateTime updated_at { get; set; }

    public DateTime? last_login { get; set; }

    public byte status { get; set; }
}
