using Kpett.ChatApp.DTOs;
using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IMessageService
    {
        Task<MessagePageResult> GetMessagesAsync(string conversationId, string currentUserId, long? cursorMessageId, int pageSize, CancellationToken cancel);
        Task MarkAsRead(string conversationId, string currentUserId, ReadMessageRequest request, CancellationToken cancel);
        Task SendMessageAsync(string conversationId, string senderId, SendMessageRequest request, CancellationToken cancel);
        Task MarkAsReadAsync(string conversationId, string userId, long lastReadMessageId, CancellationToken cancel);
    }
}
