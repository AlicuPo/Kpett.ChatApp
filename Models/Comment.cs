using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Models;

public partial class Comment
{
    public string Id { get; set; } = null!;

    public string PostId { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string? Content { get; set; }

    public string? ParentCommentId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }
}
