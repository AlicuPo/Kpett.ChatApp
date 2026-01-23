namespace Kpett.ChatApp.Services
{
    public interface IRedis
    {
        Task SaveRefreshTokenAsync(string userId, string refreshToken, TimeSpan ttl);
        Task<string?> GetRefreshTokenAsync(string userId);
        Task RemoveRefreshTokenAsync(string userId);

        Task BlacklistAccessTokenAsync(string jti, TimeSpan ttl);
        Task<bool> IsAccessTokenBlacklistedAsync(string jti);

        Task BlacklistRefreshTokenAsync(string refreshToken, TimeSpan ttl);
        Task<bool> IsRefreshTokenBlacklistedAsync(string refreshToken);
    }
}
