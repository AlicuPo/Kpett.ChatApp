using Kpett.ChatApp.DTOs;
using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Services
{
    public interface IMessage
    {
        Task<MessagePageResult> GetMessagesAsync(string conversationId, string currentUserId, long? cursorMessageId, int pageSize, CancellationToken cancel);
        Task MarkAsRead(string id, [FromBody] ReadMessageRequest request, CancellationToken cancel);      
        Task SendMessageAsync(string conversationId, string senderId, SendMessageRequest request, CancellationToken cancel);
        Task<PagedResult<MessageDTO>> GetMessagesAsync(string conversationId, long? cursorMessageId, int limit, CancellationToken cancel);
        Task MarkAsReadAsync(string conversationId, string userId, long lastReadMessageId, CancellationToken cancel);

    }
}
