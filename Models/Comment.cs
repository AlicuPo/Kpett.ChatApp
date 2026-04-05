using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class Comment
{
    public string Id { get; set; } = null!;

    [MaxLength(450)]
    public string PostId { get; set; } = null!;

    [MaxLength(450)]
    public string UserId { get; set; } = null!;
    public string? Content { get; set; }

    [MaxLength(450)]
    public string? ParentCommentId { get; set; }

    public string Path { get; set; } = null!;

    public int LikeCount { get; set; }
    public int ReplyCount { get; set; }
    public bool IsEdited { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
