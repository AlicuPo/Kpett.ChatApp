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

    public string? Role { get; set; }

    public DateTime? JoinedAt { get; set; }

    public DateTime? LastReadAt { get; set; }

    public long? LastReadMessageId { get; set; }

    public bool? IsMuted { get; set; }

    public bool? IsArchived { get; set; }
}
