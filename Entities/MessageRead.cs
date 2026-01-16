using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Entities;

public partial class MessageRead
{
    public long MessageId { get; set; }

    public Guid UserId { get; set; }

    public Guid? ConversationId { get; set; }

    public DateTime? ReadAt { get; set; }

    public virtual Conversation? Conversation { get; set; }

    public virtual Message Message { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
