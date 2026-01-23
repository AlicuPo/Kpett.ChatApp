using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Models;

public partial class Notification
{
    public string Id { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string? Type { get; set; }

    public string? Title { get; set; }

    public string? Content { get; set; }

    public string? Data { get; set; }

    public bool? IsRead { get; set; }

    public bool? IsArchived { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public string? SenderId { get; set; }
}
