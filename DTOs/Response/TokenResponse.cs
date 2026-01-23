namespace Kpett.ChatApp.DTOs.Response
{
    public class TokenResponse
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public string TokenType = "Bearer";
        public int ExpiresIn { get; set; }
        public DateTime IssuedAt = default;
        public DateTime? ExpiresAt = null;

    }





}
