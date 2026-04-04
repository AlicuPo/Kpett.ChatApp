using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class Conversation
{
    public string Id { get; set; } = null!;

    public string? Type { get; set; }

    public string? Name { get; set; }

    public string? AvatarUrl { get; set; }

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? LastMessageAt { get; set; }

    public bool? IsActive { get; set; }
}
