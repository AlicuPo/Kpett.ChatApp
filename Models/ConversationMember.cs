using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class ConversationMember
{
    public string ConversationId { get; set; } = null!;

    [MaxLength(450)]
    public string UserId { get; set; } = null!;

    public DateTime? JoinedAt { get; set; }

    public bool? IsAdmin { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
