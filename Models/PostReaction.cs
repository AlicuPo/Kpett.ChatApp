using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class PostReaction
{
    [MaxLength(450)]
    public string Id { get; set; } = null!;

    [MaxLength(450)]
    public string PostId { get; set; } = null!;

    [MaxLength(450)]
    public string UserId { get; set; } = null!;

    public byte? Type { get; set; }

    public DateTime CreatedAt { get; set; }
}
