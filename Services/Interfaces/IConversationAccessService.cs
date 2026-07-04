namespace Kpett.ChatApp.Services.Interfaces
{
    /// <summary>
    /// Service kiểm tra quyền truy cập hội thoại.
    /// </summary>
    public interface IConversationAccessService
    {
        /// <summary>Kiểm tra người dùng có quyền truy cập hội thoại không (ném ForbiddenException nếu không).</summary>
        Task EnsureCanAccessConversationAsync(string conversationId, string userId, CancellationToken cancellationToken);
    }
}
