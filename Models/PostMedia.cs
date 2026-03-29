using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class PostMedia
{
    public string Id { get; set; } = null!;

    [MaxLength(450)]
    public string? PostId { get; set; }

    public string? MediaUrl { get; set; }

    public string? MediaType { get; set; }

    public string? ThumbnailUrl { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public int? Duration { get; set; }

    public int? SortOrder { get; set; }

    public bool IsTemporary { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
