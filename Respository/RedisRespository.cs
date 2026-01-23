using StackExchange.Redis;
using Kpett.ChatApp.Services;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kpett.ChatApp.Respository
{
    public class RedisRespository : Services.IRedis
    {
        private readonly StackExchange.Redis.IDatabase _redis;
        public RedisRespository(IConnectionMultiplexer multiplexer)
        {
            _redis = multiplexer.GetDatabase();
        }
        private static string RefreshKey(string userId) => $"refresh_token:{userId}";
        private static string AccessBlacklistKey(string jti) => $"blacklist:access:{jti}";
        private static string RefreshBlacklistKey(string token) => $"blacklist:refresh:{token}";

        public async Task SaveRefreshTokenAsync(string userId, string refreshToken, TimeSpan ttl)
        {
            await _redis.StringSetAsync(
                RefreshKey(userId),
                refreshToken,
                ttl
            );
        }
        public async Task<string?> GetRefreshTokenAsync(string userId)
        {
            var value = await _redis.StringGetAsync(RefreshKey(userId));
            return value.HasValue ? value.ToString() : null;
        }

        public async Task RemoveRefreshTokenAsync(string userId)
        {
            await _redis.KeyDeleteAsync(RefreshKey(userId));
        }

        public async Task BlacklistAccessTokenAsync(string jti, TimeSpan ttl)
        {
            await _redis.StringSetAsync(
                AccessBlacklistKey(jti),
                "1",
                ttl
            );
        } 
        public async Task<bool> IsAccessTokenBlacklistedAsync(string jti)
        {
            var value = await _redis.StringGetAsync(AccessBlacklistKey(jti));
            return value.HasValue;
        }
        public async Task BlacklistRefreshTokenAsync(string refreshToken, TimeSpan ttl)
        {
            await _redis.StringSetAsync(
                RefreshBlacklistKey(refreshToken),
                "1",
                ttl
            );
        }
        public async Task<bool> IsRefreshTokenBlacklistedAsync(string refreshToken)
        {
            var value = await _redis.StringGetAsync(RefreshBlacklistKey(refreshToken));
            return value.HasValue;
        }
    }
}
