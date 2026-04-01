using StackExchange.Redis;
namespace Kpett.ChatApp.Services.Impls
{
    public class RedisService : Interfaces.IRedisService
    {
        private readonly StackExchange.Redis.IDatabase _redis;
        private readonly IConnectionMultiplexer _multiplexer;
        public RedisService(IConnectionMultiplexer multiplexer)
        {
            _multiplexer = multiplexer;
            _redis = multiplexer.GetDatabase();
        }
        private static string RefreshKey(string userId) => $"refresh_token:{userId}";
        private static string AccessBlacklistKey(string jti) => $"blacklist:access:{jti}";
        private static string RefreshBlacklistKey(string token) => $"blacklist:refresh:{token}";
        private static string UserConnectionsKey(string userId) => $"user:{userId}:connections";
        private static string ConversationUsersKey(string conversationId) => $"conversation:{conversationId}:users";
        private static string ConnectionConversationsKey(string connectionId) => $"connection:{connectionId}:conversations";

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

        // Connection / presence helpers
        public async Task AddConnectionAsync(string userId, string connectionId)
        {
            var key = UserConnectionsKey(userId);
            await _redis.ListRightPushAsync(key, connectionId);
            await _redis.KeyExpireAsync(key, TimeSpan.FromHours(24));
        }

        public async Task RemoveConnectionAsync(string userId, string connectionId)
        {
            var key = UserConnectionsKey(userId);
            await _redis.ListRemoveAsync(key, connectionId);
        }

        public async Task<string[]> GetConnectionsAsync(string userId)
        {
            var key = UserConnectionsKey(userId);
            var values = await _redis.ListRangeAsync(key);
            return values.Where(v => v.HasValue).Select(v => v.ToString()!).ToArray();
        }

        // Conversation membership helpers
        public async Task AddUserToConversationAsync(string conversationId, string userId)
        {
            await _redis.SetAddAsync(ConversationUsersKey(conversationId), userId);
        }

        public async Task RemoveUserFromConversationAsync(string conversationId, string userId)
        {
            await _redis.SetRemoveAsync(ConversationUsersKey(conversationId), userId);
        }

        public async Task<string[]> GetConversationUsersAsync(string conversationId)
        {
            var members = await _redis.SetMembersAsync(ConversationUsersKey(conversationId));
            return members.Where(m => m.HasValue).Select(m => m.ToString()!).ToArray();
        }

        public async Task TrackConnectionConversationAsync(string connectionId, string conversationId)
        {
            var key = ConnectionConversationsKey(connectionId);
            await _redis.SetAddAsync(key, conversationId);
            await _redis.KeyExpireAsync(key, TimeSpan.FromHours(24));
        }

        public async Task UntrackConnectionConversationAsync(string connectionId, string conversationId)
        {
            await _redis.SetRemoveAsync(ConnectionConversationsKey(connectionId), conversationId);
        }

        public async Task<string[]> GetConnectionConversationsAsync(string connectionId)
        {
            var members = await _redis.SetMembersAsync(ConnectionConversationsKey(connectionId));
            return members.Where(m => m.HasValue).Select(m => m.ToString()!).ToArray();
        }

        public async Task ClearConnectionConversationsAsync(string connectionId)
        {
            await _redis.KeyDeleteAsync(ConnectionConversationsKey(connectionId));
        }

        // Publish wrapper using multiplexer subscriber
        public async Task<long> PublishAsync(string channel, string message)
        {
            var sub = _multiplexer.GetSubscriber();
            return await sub.PublishAsync(RedisChannel.Literal(channel), message);
        }
    }
}
