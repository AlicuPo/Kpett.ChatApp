namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IRedisService
    {
        Task SaveRefreshTokenAsync(string userId, string refreshToken, TimeSpan ttl);
        Task<string?> GetRefreshTokenAsync(string userId);
        Task RemoveRefreshTokenAsync(string userId);

        Task BlacklistAccessTokenAsync(string jti, TimeSpan ttl);
        Task<bool> IsAccessTokenBlacklistedAsync(string jti);

        Task BlacklistRefreshTokenAsync(string refreshToken, TimeSpan ttl);
        Task<bool> IsRefreshTokenBlacklistedAsync(string refreshToken);

        // Connection / presence helpers
        Task AddConnectionAsync(string userId, string connectionId);
        Task RemoveConnectionAsync(string userId, string connectionId);
        Task<string[]> GetConnectionsAsync(string userId);
        Task<bool> IsUserOnlineAsync(string userId);
        Task<Dictionary<string, bool>> GetUsersOnlineStatusAsync(IEnumerable<string> userIds);

        // Conversation membership helpers
        Task AddUserToConversationAsync(string conversationId, string userId);
        Task RemoveUserFromConversationAsync(string conversationId, string userId);
        Task<string[]> GetConversationUsersAsync(string conversationId);
        Task TrackConnectionConversationAsync(string connectionId, string conversationId);
        Task UntrackConnectionConversationAsync(string connectionId, string conversationId);
        Task<string[]> GetConnectionConversationsAsync(string connectionId);
        Task ClearConnectionConversationsAsync(string connectionId);

        // Publish wrapper
        Task<long> PublishAsync(string channel, string message);

        // Conversation access cache (dùng cho SignalR typing indicator, tránh DB hit mỗi lần gõ phím)
        Task SetConversationAccessCacheAsync(string userId, string conversationId, TimeSpan ttl);
        Task<bool> GetConversationAccessCacheAsync(string userId, string conversationId);

        // ========== Typing Tracking ==========
        // Key: typing:{conversationId}:{userId}:{connectionId}
        // Structure: Redis SortedSet per conversation, score = expiry Unix timestamp

        /// <summary>Set/refresh typing state cho 1 connection. Score = expiry Unix timestamp.</summary>
        Task SetUserTypingAsync(string conversationId, string userId, string connectionId, TimeSpan ttl);

        /// <summary>Kiểm tra 1 connection cụ thể có đang typing không (chưa expired).</summary>
        Task<bool> IsUserConnectionTypingAsync(string conversationId, string userId, string connectionId);

        /// <summary>Xóa typing state của 1 connection.</summary>
        Task RemoveUserTypingAsync(string conversationId, string userId, string connectionId);

        /// <summary>Kiểm tra user có connection khác đang typing trong conversation (multi-tab).</summary>
        Task<bool> HasOtherTypingConnectionsAsync(string conversationId, string userId, string excludeConnectionId);

        /// <summary>Lấy danh sách (userId, connectionId) đang typing trong conversation (chưa expired).</summary>
        Task<List<(string UserId, string ConnectionId)>> GetTypingUsersInConversationAsync(string conversationId);

        /// <summary>Xóa tất cả typing state của 1 connectionId khi disconnect. Trả về (conversationId, userId) để broadcast stop.</summary>
        Task<List<(string ConversationId, string UserId)>> RemoveAllTypingForConnectionAsync(string connectionId);

    }
}
