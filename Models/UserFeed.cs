using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Models;

public partial class UserFeed
{
    public string Id { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public long PostId { get; set; }

    public string? SourceUserId { get; set; }

    public string? SourceType { get; set; }

    public DateTime? CreatedAt { get; set; }
}
