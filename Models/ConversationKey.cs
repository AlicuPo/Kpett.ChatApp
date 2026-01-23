using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Models;

public partial class ConversationKey
{
    public string Id { get; set; } = null!;

    public string? UserLowId { get; set; }

    public string? UserHighId { get; set; }

    public string? ConversationId { get; set; }
}
