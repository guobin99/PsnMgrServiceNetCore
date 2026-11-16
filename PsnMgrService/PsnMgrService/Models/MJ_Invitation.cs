using System;
using System.Collections.Generic;

namespace PsnMgrService.Models;

public partial class MJ_Invitation
{
    public int ID_KEY { get; set; }

    public string? InviterNo { get; set; }

    public string? InviterDeptName { get; set; }

    public string? OpenID { get; set; }

    public string? VisitorsName { get; set; }

    public int? InviterId { get; set; }

    public string? InviterName { get; set; }

    public string? ReservationDate { get; set; }

    public string? PlaceInvitation { get; set; }

    public string? ReasonVisit { get; set; }

    public int? NumberVisitors { get; set; }

    public string? Phone { get; set; }

    public string? Remark { get; set; }

    public int? State { get; set; }

    public int? Type { get; set; }

    public string? QRCode { get; set; }

    public string? VisitorCode { get; set; }

    public DateTime? SignInTime { get; set; }

    public DateTime? SignOutTime { get; set; }

    public DateTime? ExamineTime { get; set; }

    public DateTime? CreateTime { get; set; }

    public DateTime? ModifyTime { get; set; }

    public string? EnterpriseNo { get; set; }

    public string? Extend1 { get; set; }

    public bool? Is_Up { get; set; }
}
