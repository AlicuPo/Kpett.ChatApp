using System;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class GroupPost
{
    public string Id { get; set; } = null!;

    [MaxLength(450)]
    public string GroupId { get; set; } = null!;

    [MaxLength(450)]
    public string AuthorId { get; set; } = null!;

    public string? Content { get; set; }

    /// <summary>pending | approved | rejected</summary>
    public string? Status { get; set; }

    public bool IsPinned { get; set; }

    public DateTime? PinnedAt { get; set; }

    [MaxLength(450)]
    public string? TopicId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
