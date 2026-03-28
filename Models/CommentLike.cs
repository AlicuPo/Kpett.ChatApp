using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Models;

public partial class CommentLike
{
    public string Id { get; set; } = null!;

    public string CommentId { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }
}
