using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Models;

public partial class PostReaction
{
    public long Id { get; set; }

    public string PostId { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public byte? Type { get; set; }

    public DateTime? CreatedAt { get; set; }
}
