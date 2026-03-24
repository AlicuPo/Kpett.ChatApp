namespace Kpett.ChatApp.DTOs.Request.Auth
{
    public class LoginRequest
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string? DeviceToken { get; set; } = null!;
        public string? DeviceType { get; set; } = null!;
    }

    public class UpdateProfileRequest
    {
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
    }
    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = null!;
        public string NewPassword { get; set; } = null!;
    }
    public class UpdateUserStatusRequest
    {
        public bool IsActive { get; set; }
        public bool IsMuted { get; set; }
    }


}
