using Kpett.ChatApp.DTOs.Request.Conversation;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Conversation;
using Kpett.ChatApp.DTOs.Response.Shared;

namespace Kpett.ChatApp.Services.Abstractions
{
    /// <summary>
    /// Service quản lý hội thoại: CRUD, tin nhắn, thành viên, cài đặt (uỷ quyền message/member cho sub-services).
    /// </summary>
    public interface IConversationService
    {
        /// <summary>Tạo hội thoại mới (direct hoặc group).</summary>
        Task<ConversationResponse> CreateConversationAsync(string currentUserId, CreateConversationRequest request, CancellationToken cancel);

        /// <summary>Lấy danh sách hội thoại của người dùng (phân trang).</summary>
        Task<PaginatedData<ConversationResponse>> GetConversationsAsync(string currentUserId, ConversationListRequest request, CancellationToken cancel);

        /// <summary>Kiểm tra người dùng có hội thoại chưa đọc không.</summary>
        Task<bool> HasUnreadConversationAsync(string currentUserId, CancellationToken cancel);

        /// <summary>Thêm thành viên vào hội thoại nhóm.</summary>
        Task<bool> AddMembersToGroupAsync(string currentUserId, AddMembersRequest request, CancellationToken cancel);

        /// <summary>Xoá thành viên khỏi hội thoại nhóm.</summary>
        Task<bool> RemoveMemberFromGroupAsync(string currentUserId, string conversationId, string userIdToRemove, CancellationToken cancel);

        /// <summary>Lấy tin nhắn của hội thoại (phân trang).</summary>
        Task<PaginatedData<MessageResponse>> GetMessagesAsync(string currentUserId, string conversationId, MessageListRequest request, CancellationToken cancel);

        /// <summary>Gửi tin nhắn trong hội thoại.</summary>
        Task<MessageResponse> SendMessageAsync(string currentUserId, string conversationId, SendMessageRequest request, CancellationToken cancel);

        /// <summary>Cập nhật nội dung tin nhắn.</summary>
        Task<MessageResponse> UpdateMessageAsync(string currentUserId, string conversationId, string messageId, UpdateMessageRequest request, CancellationToken cancel);

        /// <summary>Xoá tin nhắn (soft delete).</summary>
        Task DeleteMessageAsync(string currentUserId, string conversationId, string messageId, CancellationToken cancel);

        /// <summary>Đánh dấu hội thoại đã đọc.</summary>
        Task MarkAsReadAsync(string conversationId, string currentUserId, CancellationToken cancel);

        /// <summary>Cập nhật cài đặt hội thoại (tên, ảnh, v.v.).</summary>
        Task<ConversationViewerContextResponse> UpdateConversationSettingsAsync(string currentUserId, string conversationId, UpdateConversationSettingsRequest request, CancellationToken cancel);

        /// <summary>Lấy thông tin hội thoại theo ID.</summary>
        Task<ConversationResponse> GetConversationByIdAsync(string currentUserId, string conversationId, CancellationToken cancel);

        /// <summary>Tìm hoặc tạo hội thoại direct giữa 2 người dùng.</summary>
        Task<ConversationResponse> GetOrCreateDirectConversationAsync(string currentUserId, string otherUserId, CancellationToken cancel);

        /// <summary>Lấy danh sách thành viên hội thoại nhóm.</summary>
        Task<PaginatedData<ParticipantResponse>> GetGroupMembersAsync(string currentUserId, string conversationId, CursorPaginationRequest request, CancellationToken cancel);
    }
}


