using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class Block
{
    public string Id { get; set; } = null!;

    [MaxLength(450)]
    public string? BlockerId { get; set; }

    [MaxLength(450)]
    public string? BlockedId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? Reason { get; set; }
}
