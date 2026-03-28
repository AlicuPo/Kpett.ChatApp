using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class PostReaction
{
    public long Id { get; set; }

    [MaxLength(450)]
    public string PostId { get; set; } = null!;

    [MaxLength(450)]
    public string UserId { get; set; } = null!;

    public byte? Type { get; set; }

    public DateTime? CreatedAt { get; set; }
}
