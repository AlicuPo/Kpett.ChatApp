using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class ConversationKey
{
    public string Id { get; set; } = null!;

    [MaxLength(450)]
    public string? UserLowId { get; set; }

    [MaxLength(450)]
    public string? UserHighId { get; set; }

    [MaxLength(450)]
    public string? ConversationId { get; set; }
}
