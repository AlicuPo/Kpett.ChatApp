using Kpett.ChatApp.Services.Interfaces;

namespace Kpett.ChatApp.Services.Impls
{
    public class ConversationPresenceService : IConversationPresenceService
    {
        private readonly IRedisService _redisService;

        public ConversationPresenceService(IRedisService redisService)
        {
            _redisService = redisService;
        }

        public async Task TrackConversationConnectionAsync(string conversationId, string userId, string connectionId)
        {
            await _redisService.TrackConnectionConversationAsync(connectionId, conversationId);

            try
            {
                await _redisService.AddUserToConversationAsync(conversationId, userId);
            }
            catch
            {
                await _redisService.UntrackConnectionConversationAsync(connectionId, conversationId);
                throw;
            }
        }

        public async Task UntrackConversationConnectionAsync(string conversationId, string userId, string connectionId)
        {
            await _redisService.RemoveUserFromConversationAsync(conversationId, userId);
            await _redisService.UntrackConnectionConversationAsync(connectionId, conversationId);
        }

        public async Task CleanupConnectionAsync(string? userId, string connectionId)
        {
            var conversationIds = await _redisService.GetConnectionConversationsAsync(connectionId);

            if (!string.IsNullOrWhiteSpace(userId))
            {
                foreach (var conversationId in conversationIds)
                {
                    await _redisService.RemoveUserFromConversationAsync(conversationId, userId);
                }
            }

            await _redisService.ClearConnectionConversationsAsync(connectionId);
        }
    }
}
