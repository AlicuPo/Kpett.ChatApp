using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class Friendship
{
    [MaxLength(450)]
    public string UserLowId { get; set; } = null!;

    [MaxLength(450)]
    public string UserHighId { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    [MaxLength(450)]
    public string? Status { get; set; }

    public string? ActionUserId { get; set; }
}
