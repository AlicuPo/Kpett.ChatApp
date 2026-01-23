using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Models;

public partial class Block
{
    public string Id { get; set; } = null!;

    public string? BlockerId { get; set; }

    public string? BlockedId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? Reason { get; set; }
}
