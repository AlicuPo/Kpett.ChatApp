namespace Kpett.ChatApp.DTOs.Request.Auth
{
    public class RegisterRequest
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
    }
}
