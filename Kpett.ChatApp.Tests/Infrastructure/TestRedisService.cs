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

    // Presence helpers còn thiếu
    public Task<bool> IsUserOnlineAsync(string userId)
    {
        return Task.FromResult(_connections.TryGetValue(userId, out var c) && c.Count > 0);
    }

    public Task<Dictionary<string, bool>> GetUsersOnlineStatusAsync(IEnumerable<string> userIds)
    {
        var result = userIds.ToDictionary(
            id => id,
            id => _connections.TryGetValue(id, out var c) && c.Count > 0);
        return Task.FromResult(result);
    }

    // Conversation access cache (in-memory for tests)
    private readonly HashSet<string> _conversationAccessCache = new(StringComparer.Ordinal);
    private static string ConvAccessKey(string userId, string convId) => $"{userId}::{convId}";

    public Task SetConversationAccessCacheAsync(string userId, string conversationId, TimeSpan ttl)
    {
        _conversationAccessCache.Add(ConvAccessKey(userId, conversationId));
        return Task.CompletedTask;
    }

    public Task<bool> GetConversationAccessCacheAsync(string userId, string conversationId)
    {
        return Task.FromResult(_conversationAccessCache.Contains(ConvAccessKey(userId, conversationId)));
    }

    // ========== Typing Tracking (in-memory stubs) ==========
    private readonly Dictionary<string, (string UserId, string ConnectionId, DateTime Expiry)>
        _typing = new(StringComparer.Ordinal);
    private static string TypingKey(string convId, string userId, string connId) => $"{convId}|{userId}|{connId}";

    public Task SetUserTypingAsync(string conversationId, string userId, string connectionId, TimeSpan ttl)
    {
        _typing[TypingKey(conversationId, userId, connectionId)] =
            (userId, connectionId, DateTime.UtcNow.Add(ttl));
        return Task.CompletedTask;
    }

    public Task<bool> IsUserConnectionTypingAsync(string conversationId, string userId, string connectionId)
    {
        var key = TypingKey(conversationId, userId, connectionId);
        return Task.FromResult(_typing.TryGetValue(key, out var v) && v.Expiry > DateTime.UtcNow);
    }

    public Task RemoveUserTypingAsync(string conversationId, string userId, string connectionId)
    {
        _typing.Remove(TypingKey(conversationId, userId, connectionId));
        return Task.CompletedTask;
    }

    public Task<bool> HasOtherTypingConnectionsAsync(string conversationId, string userId, string excludeConnectionId)
    {
        var now = DateTime.UtcNow;
        var prefix = $"{conversationId}|{userId}|";
        var excludeKey = TypingKey(conversationId, userId, excludeConnectionId);
        return Task.FromResult(_typing.Any(kv =>
            kv.Key.StartsWith(prefix, StringComparison.Ordinal) &&
            kv.Key != excludeKey &&
            kv.Value.Expiry > now));
    }

    public Task<List<(string UserId, string ConnectionId)>> GetTypingUsersInConversationAsync(string conversationId)
    {
        var now = DateTime.UtcNow;
        var prefix = $"{conversationId}|";
        var result = _typing
            .Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal) && kv.Value.Expiry > now)
            .Select(kv => (kv.Value.UserId, kv.Value.ConnectionId))
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<(string ConversationId, string UserId)>> RemoveAllTypingForConnectionAsync(string connectionId)
    {
        var toRemove = _typing
            .Where(kv => kv.Key.EndsWith($"|{connectionId}", StringComparison.Ordinal))
            .ToList();
        var result = new List<(string, string)>();
        foreach (var kv in toRemove)
        {
            _typing.Remove(kv.Key);
            var parts = kv.Key.Split('|');
            if (parts.Length == 3) result.Add((parts[0], parts[1]));
        }
        return Task.FromResult(result);
    }

}
