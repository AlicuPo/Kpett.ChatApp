using System;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class GroupTopic
{
    public string Id { get; set; } = null!;

    [MaxLength(450)]
    public string GroupId { get; set; } = null!;

    public string? Name { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
}
