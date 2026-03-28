namespace Kpett.ChatApp.Configs
{
    public class JwtOptions
    {
        public string KeyAccess { get; set; } = string.Empty;
        public string KeyRefres { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public int AccessTokenExpireMinutes { get; set; }
        public int RefreshTokenExpireDays { get; set; }
    }
}
