using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class UserDevice
{
    public string Id { get; set; } = null!;

    [MaxLength(450)]
    public string UserId { get; set; } = null!;

    public string? PushToken { get; set; }

    public string? DeviceType { get; set; }

    public DateTime? LastUsedAt { get; set; }
}
