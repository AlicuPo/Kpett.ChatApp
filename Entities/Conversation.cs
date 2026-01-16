using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Entities;

public partial class Conversation
{
    public Guid Id { get; set; }

    public string? ConversationName { get; set; }

    public byte ConversationType { get; set; }

    public string? AvatarUrl { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? LastMessageAt { get; set; }

    public virtual ICollection<ConversationMember> ConversationMembers { get; set; } = new List<ConversationMember>();

    public virtual User? CreatedByNavigation { get; set; }

    public virtual ICollection<MessageRead> MessageReads { get; set; } = new List<MessageRead>();

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
}
