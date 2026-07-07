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

    [MaxLength(32)]
    public string? Status { get; set; }

    public string Type { get; set; } = null!;

    public int LikeCount { get; set; }

    public int CommentCount { get; set; }

    public bool IsPinned { get; set; } = false;

    public DateTime PinnedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; } = false;

    public bool IsNsfw { get; set; } = false;
}
