using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Models;

public partial class FriendRequest
{
    public string Id { get; set; } = null!;

    public string SenderId { get; set; } = null!;

    public string ReceiverId { get; set; } = null!;

    public string? Message { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
