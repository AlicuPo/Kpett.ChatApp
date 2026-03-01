using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IConversationService
    {
        Task<List<ConversationResponse>> GetConversationList(SearchRequest search, CancellationToken cancel);
        Task<ConversationResponse> CreateConversaTion(ConversationKeysRequest request, CancellationToken cancel);
    }
}
