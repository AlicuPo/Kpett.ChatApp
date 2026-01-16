using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Entities;

public partial class Message
{
    public long Id { get; set; }

    public Guid? ConversationId { get; set; }

    public Guid? SenderId { get; set; }

    public byte? MessageType { get; set; }

    public string Content { get; set; } = null!;

    public string? AttachmentUrl { get; set; }

    public bool? IsEdited { get; set; }

    public bool? IsDeleted { get; set; }

    public bool? DeletedForAll { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Conversation? Conversation { get; set; }

    public virtual ICollection<MessageRead> MessageReads { get; set; } = new List<MessageRead>();

    public virtual User? Sender { get; set; }
}
