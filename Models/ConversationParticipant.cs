using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class ConversationParticipant
{
    public string Id { get; set; } = null!;

    [MaxLength(450)]
    public string ConversationId { get; set; } = null!;

    [MaxLength(450)]
    public string UserId { get; set; } = null!;

    public string Role { get; set; } = null!;

    public DateTime? JoinedAt { get; set; }

    public DateTime? LastReadAt { get; set; }

    [MaxLength(450)]
    public string? LastReadMessageId { get; set; }

    public bool IsMuted { get; set; } = false;

    public bool IsArchived { get; set; } = false;

    public bool IsKicked { get; set; } = false;
}
