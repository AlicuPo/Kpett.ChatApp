namespace Kpett.ChatApp.DTOs.Response
{
    public class LoginResponse : TokenResponse
    {
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
    }
  
}
