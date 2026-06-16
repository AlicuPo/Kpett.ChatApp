using System;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class GroupInvitation
{
    public string Id { get; set; } = null!;

    [MaxLength(450)]
    public string GroupId { get; set; } = null!;

    [MaxLength(450)]
    public string InvitedByUserId { get; set; } = null!;

    [MaxLength(450)]
    public string InviteeUserId { get; set; } = null!;

    /// <summary>pending | accepted | declined</summary>
    public string? Status { get; set; }

    public DateTime CreatedAt { get; set; }
}
