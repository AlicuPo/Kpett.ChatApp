using Kpett.ChatApp.DTOs.Request.Message;
using Kpett.ChatApp.DTOs.Response.Message;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IMessageService
    {
        Task<MessagePageResult> GetMessagesAsync(string conversationId, string currentUserId, long? cursorMessageId, int pageSize, CancellationToken cancel);
        Task<MessageDTO> SendMessageAsync(string conversationId, string senderId, SendMessageRequest request, CancellationToken cancel);
        Task MarkAsReadAsync(string conversationId, string currentUserId, ReadMessageRequest request, CancellationToken cancel);
    }
}
