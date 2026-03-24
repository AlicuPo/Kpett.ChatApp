using Kpett.ChatApp.DTOs.Response.User;

namespace Kpett.ChatApp.DTOs.Response.Auth
{
    public class LoginResponse : TokenResponse
    {
        public UserResponse User { get; set; } = null!;
        public TokenResponse Token { get; set; } = null!;
    }
  
}
