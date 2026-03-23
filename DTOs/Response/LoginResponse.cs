namespace Kpett.ChatApp.DTOs.Response
{
    public class LoginResponse : TokenResponse
    {
        public UserResponse User { get; set; } = null!;
        public TokenResponse Token { get; set; } = null!;
    }
  
}
