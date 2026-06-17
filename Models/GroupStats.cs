using System;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class GroupStats
{
    [MaxLength(450)]
    public string GroupId { get; set; } = null!;

    public DateOnly Date { get; set; }

    public int MemberCount { get; set; }

    public int PostCount { get; set; }

    public int InteractionCount { get; set; }
}
