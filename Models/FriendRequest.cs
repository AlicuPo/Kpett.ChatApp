using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class FriendRequest
{
    public string Id { get; set; } = null!;

    [MaxLength(450)]
    public string SenderId { get; set; } = null!;

    [MaxLength(450)]
    public string ReceiverId { get; set; } = null!;

    public string? Message { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
