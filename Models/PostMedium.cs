using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Models;

public partial class PostMedium
{
    public string Id { get; set; } = null!;

    public long PostId { get; set; }

    public string? MediaUrl { get; set; }

    public string? MediaType { get; set; }

    public string? ThumbnailUrl { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public int? Duration { get; set; }

    public int? SortOrder { get; set; }
}
