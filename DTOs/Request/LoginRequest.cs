namespace Kpett.ChatApp.DTOs.Request
{
    public class LoginRequest
    {
        public string UsernameOrEmail { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string? DeviceToken { get; set; } = null!;
        public string? DeviceType { get; set; } = null!;
    }

    public record LogoutRequest(bool LogoutAllDevices = false);

    public class RegisterRequest
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }     
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
