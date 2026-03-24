namespace Kpett.ChatApp.DTOs.Request.Auth
{
    public class LogoutRequest() 
    {
        public string RefreshToken { get; set; } = null!;
    }
}
