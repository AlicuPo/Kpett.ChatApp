using Kpett.ChatApp.DTOs.Request.Conversation;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Conversation;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IConversationService
    {
        Task<List<ConversationResponse>> GetConversationList(string currentUserId, SearchRequest search, CancellationToken cancel);
        Task<ConversationResponse> CreateConversaTion(string currentUserId, ConversationKeysRequest request, CancellationToken cancel);
    }
}
