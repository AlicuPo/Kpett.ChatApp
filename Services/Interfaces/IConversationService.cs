using Kpett.ChatApp.DTOs.Request.Conversation;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Conversation;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IConversationService
    {
        Task<List<ConversationResponse>> GetConversationsAsync(string currentUserId, SearchRequest search, CancellationToken cancel);
        Task<ConversationResponse> CreateConversationAsync(string currentUserId, ConversationKeysRequest request, CancellationToken cancel);
    }
}
