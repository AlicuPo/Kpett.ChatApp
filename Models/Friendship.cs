using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Models;

public partial class Friendship
{
    public string UserLowId { get; set; } = null!;

    public string UserHighId { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? Status { get; set; }

    public string? ActionUserId { get; set; }
}
