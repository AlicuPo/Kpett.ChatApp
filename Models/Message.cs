using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Models;

public partial class Message
{
    public long Id { get; set; }

    public string ConversationId { get; set; } = null!;

    public string SenderId { get; set; } = null!;

    public string? ClientMessageId { get; set; }

    public string? Metadata { get; set; }

    public string? Type { get; set; }

    public DateTime? CreatedAt { get; set; }

    public bool? IsDeleted { get; set; }
}
