namespace Kpett.ChatApp.Response
{
    public class LoginResponse
    {
        public string AccessToken { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
    }
}
