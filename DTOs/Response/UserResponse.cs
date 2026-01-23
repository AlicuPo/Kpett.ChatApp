namespace Kpett.ChatApp.DTOs.Response
{
    public class UserResponse
    {
        public string Id { get; set; }
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
        public DateTime? LastActiveAt { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

}
