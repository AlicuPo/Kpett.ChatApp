using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Models;

public partial class ConversationParticipant
{
    public string Id { get; set; } = null!;

    public string ConversationId { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string? Role { get; set; }

    public DateTime? JoinedAt { get; set; }

    public DateTime? LastReadAt { get; set; }

    public long? LastReadMessageId { get; set; }

    public bool? IsMuted { get; set; }

    public bool? IsArchived { get; set; }
}
