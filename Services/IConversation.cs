using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Services
{
    public interface IConversation
    {
        Task<List<ConversationResponse>> GetConversationList(SearchRequest search, CancellationToken cancel);
        //Task<List<ConversationResponse>> CreateConversaTion(CancellationToken cancel);
    }
}
