namespace Kpett.ChatApp.DTOs.Response
{
    public class TokenResponse
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public string TokenType { get; set; } = "Bearer";
        public int ExpiresIn { get; set; } = 30 * 60;
        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
    }





}
