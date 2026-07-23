using Kpett.ChatApp.DTOs.Request.Conversation;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Conversation;
using Kpett.ChatApp.DTOs.Response.Shared;

namespace Kpett.ChatApp.Services.Abstractions
{
    /// <summary>
    /// Service quản lý tin nhắn trong hội thoại: lấy, gửi, cập nhật, xoá.
    /// </summary>
    public interface IConversationMessageService
    {
        /// <summary>Lấy tin nhắn của hội thoại (phân trang).</summary>
        Task<PaginatedData<MessageResponse>> GetMessagesAsync(string currentUserId, string conversationId, MessageListRequest request, CancellationToken cancel);

        /// <summary>Gửi tin nhắn mới trong hội thoại.</summary>
        Task<MessageResponse> SendMessageAsync(string currentUserId, string conversationId, SendMessageRequest request, CancellationToken cancel);

        /// <summary>Cập nhật nội dung tin nhắn.</summary>
        Task<MessageResponse> UpdateMessageAsync(string currentUserId, string conversationId, string messageId, UpdateMessageRequest request, CancellationToken cancel);

        /// <summary>Xoá tin nhắn (soft delete).</summary>
        Task DeleteMessageAsync(string currentUserId, string conversationId, string messageId, CancellationToken cancel);
    }
}


