namespace Kpett.ChatApp.DTOs.Response.Auth
{
    public class TokenResponse
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public string TokenType { get; set; } = "Bearer";
    }
}
