using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Models;

public partial class Post
{
    public long Id { get; set; }

    public string CreatedByUserId { get; set; } = null!;

    public string? Content { get; set; }

    public string? Privacy { get; set; }

    public string? GroupId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool? IsDeleted { get; set; }
}
