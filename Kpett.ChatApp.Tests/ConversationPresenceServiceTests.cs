using Kpett.ChatApp.Services.Impls;
using Kpett.ChatApp.Services.Interfaces;

namespace Kpett.ChatApp.Tests;

public class ConversationPresenceServiceTests
{
    [Fact]
    public async Task CleanupConnectionAsync_RemovesUserFromTrackedConversationsAndClearsTracking()
    {
        var redis = new FakeRedisService();
        var service = new ConversationPresenceService(redis);

        await service.TrackConversationConnectionAsync("conversation-1", "user-1", "connection-1");
        await service.TrackConversationConnectionAsync("conversation-2", "user-1", "connection-1");

        Assert.Equal(new[] { "user-1" }, await redis.GetConversationUsersAsync("conversation-1"));
        Assert.Equal(new[] { "user-1" }, await redis.GetConversationUsersAsync("conversation-2"));
        Assert.Equal(
            new[] { "conversation-1", "conversation-2" },
            (await redis.GetConnectionConversationsAsync("connection-1")).OrderBy(x => x).ToArray());

        await service.CleanupConnectionAsync("user-1", "connection-1");

        Assert.Empty(await redis.GetConversationUsersAsync("conversation-1"));
        Assert.Empty(await redis.GetConversationUsersAsync("conversation-2"));
        Assert.Empty(await redis.GetConnectionConversationsAsync("connection-1"));
    }

    private sealed class FakeRedisService : IRedisService
    {
        private readonly Dictionary<string, HashSet<string>> _conversationUsers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _connectionConversations = new(StringComparer.Ordinal);

        public Task SaveRefreshTokenAsync(string userId, string refreshToken, TimeSpan ttl) => Task.CompletedTask;
        public Task<string?> GetRefreshTokenAsync(string userId) => Task.FromResult<string?>(null);
        public Task RemoveRefreshTokenAsync(string userId) => Task.CompletedTask;
        public Task BlacklistAccessTokenAsync(string jti, TimeSpan ttl) => Task.CompletedTask;
        public Task<bool> IsAccessTokenBlacklistedAsync(string jti) => Task.FromResult(false);
        public Task BlacklistRefreshTokenAsync(string refreshToken, TimeSpan ttl) => Task.CompletedTask;
        public Task<bool> IsRefreshTokenBlacklistedAsync(string refreshToken) => Task.FromResult(false);
        public Task AddConnectionAsync(string userId, string connectionId) => Task.CompletedTask;
        public Task RemoveConnectionAsync(string userId, string connectionId) => Task.CompletedTask;
        public Task<string[]> GetConnectionsAsync(string userId) => Task.FromResult(Array.Empty<string>());

        public Task AddUserToConversationAsync(string conversationId, string userId)
        {
            if (!_conversationUsers.TryGetValue(conversationId, out var users))
            {
                users = new HashSet<string>(StringComparer.Ordinal);
                _conversationUsers[conversationId] = users;
            }

            users.Add(userId);
            return Task.CompletedTask;
        }

        public Task RemoveUserFromConversationAsync(string conversationId, string userId)
        {
            if (_conversationUsers.TryGetValue(conversationId, out var users))
            {
                users.Remove(userId);
            }

            return Task.CompletedTask;
        }

        public Task<string[]> GetConversationUsersAsync(string conversationId)
        {
            return Task.FromResult(
                _conversationUsers.TryGetValue(conversationId, out var users)
                    ? users.OrderBy(x => x).ToArray()
                    : Array.Empty<string>());
        }

        public Task TrackConnectionConversationAsync(string connectionId, string conversationId)
        {
            if (!_connectionConversations.TryGetValue(connectionId, out var conversations))
            {
                conversations = new HashSet<string>(StringComparer.Ordinal);
                _connectionConversations[connectionId] = conversations;
            }

            conversations.Add(conversationId);
            return Task.CompletedTask;
        }

        public Task UntrackConnectionConversationAsync(string connectionId, string conversationId)
        {
            if (_connectionConversations.TryGetValue(connectionId, out var conversations))
            {
                conversations.Remove(conversationId);
            }

            return Task.CompletedTask;
        }

        public Task<string[]> GetConnectionConversationsAsync(string connectionId)
        {
            return Task.FromResult(
                _connectionConversations.TryGetValue(connectionId, out var conversations)
                    ? conversations.OrderBy(x => x).ToArray()
                    : Array.Empty<string>());
        }

        public Task ClearConnectionConversationsAsync(string connectionId)
        {
            _connectionConversations.Remove(connectionId);
            return Task.CompletedTask;
        }

        public Task<long> PublishAsync(string channel, string message) => Task.FromResult(0L);
    }
}
