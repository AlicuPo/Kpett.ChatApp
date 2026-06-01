using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public class Notification
{
    public string Id { get; set; } = null!;

    [Required]
    [MaxLength(450)]
    public string RecipientId { get; set; } = null!;

    [Required]
    [MaxLength(450)]
    public string ActorId { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = null!;

    [MaxLength(450)]
    public string? ReferenceId { get; set; }

    public string? Metadata { get; set; }

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
