using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Entities;

public partial class ConversationMember
{
    public Guid ConversationId { get; set; }

    public Guid UserId { get; set; }

    public DateTime? JoinedAt { get; set; }

    public bool? IsAdmin { get; set; }

    public bool? IsMuted { get; set; }

    public string? Nickname { get; set; }

    public DateTime? LastSeenAt { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
