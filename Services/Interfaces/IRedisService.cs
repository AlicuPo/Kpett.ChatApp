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

        // Conversation membership helpers
        Task AddUserToConversationAsync(string conversationId, string userId);
        Task RemoveUserFromConversationAsync(string conversationId, string userId);
        Task<string[]> GetConversationUsersAsync(string conversationId);

        // Publish wrapper
        Task<long> PublishAsync(string channel, string message);
    }
}
