namespace Kpett.ChatApp.DTOs.Request
{
    public class UpdateUserRequest
    {
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Phone { get; set; }
        public string? Gender { get; set; }
    }
}
