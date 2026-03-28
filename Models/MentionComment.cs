using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Models;

public partial class MentionComment
{
    public string Id { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string CommentId { get; set; } = null!;

    public string Username { get; set; } = null!;

    public string? DisplayName { get; set; }

    public bool IsNotified { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
