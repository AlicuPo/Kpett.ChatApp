using Kpett.ChatApp.DTOs.Request.Conversation;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Conversation;
using Kpett.ChatApp.DTOs.Response.Shared;

namespace Kpett.ChatApp.Services.Interfaces
{
    /// <summary>
    /// Service quản lý thành viên hội thoại nhóm: thêm, xoá, lấy danh sách.
    /// </summary>
    public interface IConversationMemberService
    {
        /// <summary>Thêm thành viên vào hội thoại nhóm.</summary>
        Task<bool> AddMembersToGroupAsync(string currentUserId, AddMembersRequest request, CancellationToken cancel);

        /// <summary>Xoá thành viên khỏi hội thoại nhóm.</summary>
        Task<bool> RemoveMemberFromGroupAsync(string currentUserId, string conversationId, string userIdToRemove, CancellationToken cancel);

        /// <summary>Lấy danh sách thành viên hội thoại (phân trang cursor).</summary>
        Task<PaginatedData<ParticipantResponse>> GetGroupMembersAsync(string currentUserId, string conversationId, CursorPaginationRequest request, CancellationToken cancel);
    }
}
