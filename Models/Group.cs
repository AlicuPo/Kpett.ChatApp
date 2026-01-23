using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Models;

public partial class Group
{
    public string Id { get; set; } = null!;

    public string? Name { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Description { get; set; }

    public string? Type { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? CreatedByUserId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedByUserId { get; set; }
}
