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

        // ===== Typing tracking keys =====
        // SortedSet per conversation: member = "userId:connectionId", score = Unix expiry timestamp
        private static string ConvTypingSetKey(string conversationId) => $"typing:conv:{conversationId}";
        // Reverse index: connection -> set of "conversationId|userId|connectionId"
        private static string ConnTypingSetKey(string connectionId) => $"typing:conn:{connectionId}";
        private static string TypingMember(string userId, string connectionId) => $"{userId}:{connectionId}";
        private const string TypingEntrySeparator = "|"; // dùng trong ConnTypingSetKey

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

        public async Task<bool> IsUserOnlineAsync(string userId)
        {
            var key = UserConnectionsKey(userId);
            var count = await _redis.ListLengthAsync(key);
            return count > 0;
        }

        public async Task<Dictionary<string, bool>> GetUsersOnlineStatusAsync(IEnumerable<string> userIds)
        {
            var result = new Dictionary<string, bool>();


            var batch = _redis.CreateBatch();
            var tasks = new Dictionary<string, Task<long>>();

            foreach (var userId in userIds)
            {
                var key = UserConnectionsKey(userId);
                tasks[userId] = batch.ListLengthAsync(key);
            }

            batch.Execute();

            foreach (var task in tasks)
            {
                result[task.Key] = (await task.Value) > 0;
            }

            return result;
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

        // Conversation access cache
        // Key: user:{userId}:conv_access:{conversationId}, Value: "1", TTL: configurable
        private static string ConversationAccessKey(string userId, string conversationId)
            => $"user:{userId}:conv_access:{conversationId}";

        public async Task SetConversationAccessCacheAsync(string userId, string conversationId, TimeSpan ttl)
        {
            await _redis.StringSetAsync(ConversationAccessKey(userId, conversationId), "1", ttl);
        }

        public async Task<bool> GetConversationAccessCacheAsync(string userId, string conversationId)
        {
            var value = await _redis.StringGetAsync(ConversationAccessKey(userId, conversationId));
            return value.HasValue;
        }

        // ========================================================================
        // TYPING TRACKING
        // ========================================================================

        public async Task SetUserTypingAsync(string conversationId, string userId, string connectionId, TimeSpan ttl)
        {
            var expiryUnix = DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeSeconds();
            var member = TypingMember(userId, connectionId);

            // 1. Thêm/cập nhật vào SortedSet của conversation (score = Unix expiry)
            var convKey = ConvTypingSetKey(conversationId);
            await _redis.SortedSetAddAsync(convKey, member, expiryUnix);
            // Đặt TTL cho SortedSet = max TTL + buffer (tự dọn khi conversation không còn ai typing)
            await _redis.KeyExpireAsync(convKey, ttl + TimeSpan.FromSeconds(30));

            // 2. Thêm vào reverse index connection → conversations (để cleanup khi disconnect)
            var entry = $"{conversationId}{TypingEntrySeparator}{userId}{TypingEntrySeparator}{connectionId}";
            var connKey = ConnTypingSetKey(connectionId);
            await _redis.SetAddAsync(connKey, entry);
            await _redis.KeyExpireAsync(connKey, ttl + TimeSpan.FromSeconds(30));
        }

        public async Task<bool> IsUserConnectionTypingAsync(string conversationId, string userId, string connectionId)
        {
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var member = TypingMember(userId, connectionId);
            var score = await _redis.SortedSetScoreAsync(ConvTypingSetKey(conversationId), member);
            // Member tồn tại VÀ chưa expired
            return score.HasValue && score.Value > nowUnix;
        }

        public async Task RemoveUserTypingAsync(string conversationId, string userId, string connectionId)
        {
            var member = TypingMember(userId, connectionId);
            await _redis.SortedSetRemoveAsync(ConvTypingSetKey(conversationId), member);

            var entry = $"{conversationId}{TypingEntrySeparator}{userId}{TypingEntrySeparator}{connectionId}";
            await _redis.SetRemoveAsync(ConnTypingSetKey(connectionId), entry);
        }

        public async Task<bool> HasOtherTypingConnectionsAsync(string conversationId, string userId, string excludeConnectionId)
        {
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // Lấy tất cả member chưa expired trong conversation
            var activeMembers = await _redis.SortedSetRangeByScoreAsync(
                ConvTypingSetKey(conversationId),
                start: nowUnix, stop: double.PositiveInfinity);

            // Kiểm tra có member nào của cùng userId nhưng connectionId khác không
            var prefix = $"{userId}:";
            var excludeMember = TypingMember(userId, excludeConnectionId);

            return activeMembers.Any(m =>
                m.ToString().StartsWith(prefix, StringComparison.Ordinal) &&
                m.ToString() != excludeMember);
        }

        public async Task<List<(string UserId, string ConnectionId)>> GetTypingUsersInConversationAsync(string conversationId)
        {
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Dọn dẹp expired entries trước
            await _redis.SortedSetRemoveRangeByScoreAsync(ConvTypingSetKey(conversationId),
                double.NegativeInfinity, nowUnix - 1);

            var members = await _redis.SortedSetRangeByScoreAsync(
                ConvTypingSetKey(conversationId),
                start: nowUnix, stop: double.PositiveInfinity);

            var result = new List<(string UserId, string ConnectionId)>();
            foreach (var m in members)
            {
                var parts = m.ToString().Split(':', 2);
                if (parts.Length == 2)
                    result.Add((parts[0], parts[1]));
            }
            return result;
        }

        public async Task<List<(string ConversationId, string UserId)>> RemoveAllTypingForConnectionAsync(string connectionId)
        {
            var connKey = ConnTypingSetKey(connectionId);
            var entries = await _redis.SetMembersAsync(connKey);
            var removed = new List<(string ConversationId, string UserId)>();

            foreach (var entry in entries)
            {
                var parts = entry.ToString().Split(TypingEntrySeparator, 3);
                if (parts.Length != 3) continue;

                var (convId, userId, connId) = (parts[0], parts[1], parts[2]);
                await _redis.SortedSetRemoveAsync(ConvTypingSetKey(convId), TypingMember(userId, connId));
                removed.Add((convId, userId));
            }

            await _redis.KeyDeleteAsync(connKey);
            return removed;
        }

    }
}
