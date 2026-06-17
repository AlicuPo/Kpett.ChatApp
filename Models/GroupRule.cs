using System;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class GroupRule
{
    public string Id { get; set; } = null!;

    [MaxLength(450)]
    public string GroupId { get; set; } = null!;

    public string? Title { get; set; }

    public string? Description { get; set; }

    public int Order { get; set; }
}
