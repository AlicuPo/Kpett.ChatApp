using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class Post
{
    public string Id { get; set; } = null!;

    [MaxLength(450)]
    public string CreatedByUserId { get; set; } = null!;

    public string? Content { get; set; }

    public string? Privacy { get; set; }

    [MaxLength(450)]
    public string? GroupId { get; set; }

    public string? Type { get; set; }

    public bool IsPinned { get; set; } = false;

    public DateTime PinnedAt { get; set; } = DateTime.Now;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; } = false;
}
