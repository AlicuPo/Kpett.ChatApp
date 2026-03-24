using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Models;

public partial class User
{
    public string Id { get; set; } = null!;
    public string? Username { get; set; } = null!;
    public string? Gender { get; set; }
    public string? Email { get; set; }
    public bool? EmailConfirmed { get; set; }
    public string? Phone { get; set; }
    public bool? PhoneConfirmed { get; set; }
    public string Password { get; set; } = null!;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? CoverUrl { get; set; }
    public string? Biography { get; set; }
    public string? Location { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Cocupation { get; set; }
    public string? Interests { get; set; }
    public string? SocialLinks { get; set; }
    public bool IsVerified { get; set; }
    public bool IsAccountPrivate { get; set; }
    public string? Status { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

}
