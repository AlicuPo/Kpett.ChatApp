using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class Message
{
    public long Id { get; set; }

    [MaxLength(450)]
    public string ConversationId { get; set; } = null!;

    [MaxLength(450)]
    public string SenderId { get; set; } = null!;

    [MaxLength(450)]
    public string? ClientMessageId { get; set; }

    public string? Metadata { get; set; }

    public string? Type { get; set; }

    public DateTime? CreatedAt { get; set; }

    public bool? IsDeleted { get; set; }
}
