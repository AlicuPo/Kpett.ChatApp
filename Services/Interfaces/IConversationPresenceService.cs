namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IConversationPresenceService
    {
        Task TrackConversationConnectionAsync(string conversationId, string userId, string connectionId);
        Task UntrackConversationConnectionAsync(string conversationId, string userId, string connectionId);
        Task CleanupConnectionAsync(string? userId, string connectionId);
    }
}
