using Kpett.ChatApp.Services.Abstractions;

namespace Kpett.ChatApp.Services.Implementations;

public class ConversationTypingService : IConversationTypingService
{
    private static readonly TimeSpan TypingTtl = TimeSpan.FromSeconds(5);
    private readonly IRedisService _redis;
    private readonly ILogger<ConversationTypingService> _logger;

    public ConversationTypingService(
        IRedisService redis,
        ILogger<ConversationTypingService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<bool> StartTypingAsync(string conversationId, string userId, string connectionId, CancellationToken ct = default)
    {
        try
        {
            var typingUsers = await _redis.GetTypingUsersInConversationAsync(conversationId);
            var thisConnectionAlreadyTyping = typingUsers.Any(t =>
                t.UserId == userId && t.ConnectionId == connectionId);
            var userAlreadyTypingFromOtherTab = typingUsers.Any(t =>
                t.UserId == userId && t.ConnectionId != connectionId);

            await _redis.SetUserTypingAsync(conversationId, userId, connectionId, TypingTtl);

            return !thisConnectionAlreadyTyping && !userAlreadyTypingFromOtherTab;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting typing for user {UserId} in conversation {ConversationId}", userId, conversationId);
            return false;
        }
    }

    public async Task<bool> StopTypingAsync(string conversationId, string userId, string connectionId, CancellationToken ct = default)
    {
        try
        {
            await _redis.RemoveUserTypingAsync(conversationId, userId, connectionId);

            var hasOtherConnections = await _redis.HasOtherTypingConnectionsAsync(conversationId, userId, connectionId);
            return !hasOtherConnections;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping typing for user {UserId} in conversation {ConversationId}", userId, conversationId);
            return false;
        }
    }

    public async Task<List<(string UserId, string ConnectionId)>> GetTypingUsersAsync(string conversationId)
    {
        try
        {
            return await _redis.GetTypingUsersInConversationAsync(conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting typing users for conversation {ConversationId}", conversationId);
            return new List<(string UserId, string ConnectionId)>();
        }
    }

    public async Task<List<(string ConversationId, string UserId)>> CleanupConnectionTypingAsync(string connectionId)
    {
        try
        {
            return await _redis.RemoveAllTypingForConnectionAsync(connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up typing state for connection {ConnectionId}", connectionId);
            return new List<(string ConversationId, string UserId)>();
        }
    }
}
