using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class GroupNotificationSetting
{
    public string Id { get; set; } = null!;

    [MaxLength(450)]
    public string GroupId { get; set; } = null!;

    [MaxLength(450)]
    public string UserId { get; set; } = null!;

    /// <summary>all | highlights | off</summary>
    public string? Level { get; set; }
}
