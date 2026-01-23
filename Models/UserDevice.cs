using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Models;

public partial class UserDevice
{
    public string Id { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string? PushToken { get; set; }

    public string? DeviceType { get; set; }

    public DateTime? LastUsedAt { get; set; }
}
