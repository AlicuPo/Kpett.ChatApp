using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class CommentLike
{
    public string Id { get; set; } = null!;

    [MaxLength(450)]
    public string CommentId { get; set; } = null!;

    [MaxLength(450)]
    public string UserId { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }
}
