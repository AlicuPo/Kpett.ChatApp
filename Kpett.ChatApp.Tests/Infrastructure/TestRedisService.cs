using Kpett.ChatApp.Services.Interfaces;

namespace Kpett.ChatApp.Tests.Infrastructure;

public class TestRedisService : IRedisService
{
    private readonly Dictionary<string, string> _refreshTokens = new(StringComparer.Ordinal);
    private readonly HashSet<string> _accessBlacklist = new(StringComparer.Ordinal);
    private readonly HashSet<string> _refreshBlacklist = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _connections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _conversationUsers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _connectionConversations = new(StringComparer.Ordinal);

    public Task SaveRefreshTokenAsync(string userId, string refreshToken, TimeSpan ttl)
    {
        _refreshTokens[userId] = refreshToken;
        return Task.CompletedTask;
    }

    public Task<string?> GetRefreshTokenAsync(string userId)
    {
        _refreshTokens.TryGetValue(userId, out var refreshToken);
        return Task.FromResult<string?>(refreshToken);
    }

    public Task RemoveRefreshTokenAsync(string userId)
    {
        _refreshTokens.Remove(userId);
        return Task.CompletedTask;
    }

    public Task BlacklistAccessTokenAsync(string jti, TimeSpan ttl)
    {
        _accessBlacklist.Add(jti);
        return Task.CompletedTask;
    }

    public Task<bool> IsAccessTokenBlacklistedAsync(string jti)
    {
        return Task.FromResult(_accessBlacklist.Contains(jti));
    }

    public Task BlacklistRefreshTokenAsync(string refreshToken, TimeSpan ttl)
    {
        _refreshBlacklist.Add(refreshToken);
        return Task.CompletedTask;
    }

    public Task<bool> IsRefreshTokenBlacklistedAsync(string refreshToken)
    {
        return Task.FromResult(_refreshBlacklist.Contains(refreshToken));
    }

    public Task AddConnectionAsync(string userId, string connectionId)
    {
        if (!_connections.TryGetValue(userId, out var connections))
        {
            connections = new HashSet<string>(StringComparer.Ordinal);
            _connections[userId] = connections;
        }

        connections.Add(connectionId);
        return Task.CompletedTask;
    }

    public Task RemoveConnectionAsync(string userId, string connectionId)
    {
        if (_connections.TryGetValue(userId, out var connections))
        {
            connections.Remove(connectionId);
        }

        return Task.CompletedTask;
    }

    public Task<string[]> GetConnectionsAsync(string userId)
    {
        return Task.FromResult(
            _connections.TryGetValue(userId, out var connections)
                ? connections.ToArray()
                : Array.Empty<string>());
    }

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
                ? users.ToArray()
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
                ? conversations.ToArray()
                : Array.Empty<string>());
    }

    public Task ClearConnectionConversationsAsync(string connectionId)
    {
        _connectionConversations.Remove(connectionId);
        return Task.CompletedTask;
    }

    public Task<long> PublishAsync(string channel, string message)
    {
        return Task.FromResult(0L);
    }
}
