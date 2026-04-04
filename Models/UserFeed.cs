using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class UserFeed
{
    public string Id { get; set; } = null!;

    [MaxLength(450)]
    public string UserId { get; set; } = null!;

    [MaxLength(450)]
    public string PostId { get; set; } = null!;

    [MaxLength(450)]
    public string? SourceUserId { get; set; }

    public string? SourceType { get; set; }

    public DateTime CreatedAt { get; set; }
}
