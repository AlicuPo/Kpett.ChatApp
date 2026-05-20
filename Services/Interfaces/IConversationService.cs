using Kpett.ChatApp.DTOs.Request.Conversation;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Conversation;
using Kpett.ChatApp.DTOs.Response.Shared;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IConversationService
    {
        Task<ConversationResponse> CreateConversationAsync(string currentUserId, CreateConversationRequest request, CancellationToken cancel);
        Task<PaginatedData<ConversationResponse>> GetConversationsAsync(string currentUserId, ConversationListRequest request, CancellationToken cancel);
        Task<bool> HasUnreadConversationAsync(string currentUserId, CancellationToken cancel);
        Task<bool> AddMembersToGroupAsync(string currentUserId, AddMembersRequest request, CancellationToken cancel);
        Task<bool> RemoveMemberFromGroupAsync(string currentUserId, string conversationId, string userIdToRemove, CancellationToken cancel);
        Task<PaginatedData<MessageResponse>> GetMessagesAsync(string currentUserId, string conversationId, MessageListRequest request, CancellationToken cancel);
        Task<MessageResponse> SendMessageAsync(string currentUserId, string conversationId, SendMessageRequest request, CancellationToken cancel);
        Task<MessageResponse> UpdateMessageAsync(string currentUserId, string conversationId, string messageId, UpdateMessageRequest request, CancellationToken cancel);
        Task DeleteMessageAsync(string currentUserId, string conversationId, string messageId, CancellationToken cancel);
        Task MarkAsReadAsync(string conversationId, string currentUserId, CancellationToken cancel);
        Task<ConversationViewerContextResponse> UpdateConversationSettingsAsync(string currentUserId, string conversationId, UpdateConversationSettingsRequest request, CancellationToken cancel);
        Task<ConversationResponse> GetConversationByIdAsync(string currentUserId, string conversationId, CancellationToken cancel);
        Task<ConversationResponse> GetOrCreateDirectConversationAsync(string currentUserId, string otherUserId, CancellationToken cancel);
        Task<PaginatedData<ParticipantResponse>> GetGroupMembersAsync(string currentUserId, string conversationId, CursorPaginationRequest request, CancellationToken cancel);
    }
}
