using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Models;

public partial class UserSetting
{
    public string Id { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string? SettingType { get; set; }

    public string? SettingKey { get; set; }

    public string? SettingValue { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
