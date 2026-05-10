using Kpett.ChatApp.DTOs.Response.Conversation;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface ITypingService
    {
        /// <summary>
        /// Bắt đầu hoặc refresh typing session cho 1 connection.
        /// Trả về true nếu user chưa typing từ bất kỳ tab nào trước đó (cần broadcast start).
        /// </summary>
        Task<bool> StartTypingAsync(string conversationId, string userId, string connectionId, CancellationToken ct = default);

        /// <summary>
        /// Dừng typing session cho 1 connection (explicit stop).
        /// Trả về true nếu không còn tab nào của user typing (cần broadcast stop).
        /// </summary>
        Task<bool> StopTypingAsync(string conversationId, string userId, string connectionId, CancellationToken ct = default);

        /// <summary>
        /// Lấy danh sách user đang typing trong conversation, lọc expired.
        /// </summary>
        Task<List<(string UserId, string ConnectionId)>> GetTypingUsersAsync(string conversationId);

        /// <summary>
        /// Dọn dẹp typing state của 1 connection khi disconnect.
        /// Trả về map (conversationId, userId) để caller tự broadcast stop.
        /// </summary>
        Task<List<(string ConversationId, string UserId)>> CleanupConnectionTypingAsync(string connectionId);

    }
}
