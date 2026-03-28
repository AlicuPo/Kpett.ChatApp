using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class GroupMember
{
    public string Id { get; set; } = null!;

    [MaxLength(450)]
    public string UserId { get; set; } = null!;

    [MaxLength(450)]
    public string GroupId { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    [MaxLength(450)]
    public string? UpdatedByUserId { get; set; }

    public string? Role { get; set; }

    public string? Status { get; set; }

    public DateTime? JoinedAt { get; set; }
}
