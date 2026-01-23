using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Models;

public partial class GroupMember
{
    public string Id { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string GroupId { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public string? CreatedByUserId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedByUserId { get; set; }

    public string? Role { get; set; }

    public string? Status { get; set; }

    public DateTime? JoinedAt { get; set; }
}
