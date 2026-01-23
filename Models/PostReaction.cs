using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Models;

public partial class PostReaction
{
    public long Id { get; set; }

    public long PostId { get; set; }

    public string UserId { get; set; } = null!;

    public byte? Type { get; set; }

    public DateTime? CreatedAt { get; set; }
}
