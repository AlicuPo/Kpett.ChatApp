using Kpett.ChatApp.DTOs.Response.User;

namespace Kpett.ChatApp.DTOs.Response.Auth
{
    public class LoginResponse
    {
        public UserResponse User { get; set; } = null!;
        public TokenResponse Token { get; set; } = null!;
    }
  
}
