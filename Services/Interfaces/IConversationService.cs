using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IConversationService
    {
        Task<List<ConversationResponse>> GetConversationList(string currentUserId, SearchRequest search, CancellationToken cancel);
        Task<ConversationResponse> CreateConversaTion(string currentUserId, ConversationKeysRequest request, CancellationToken cancel);
    }
}
