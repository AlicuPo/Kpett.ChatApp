using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Message;
using Kpett.ChatApp.DTOs.Response.Notification;
using Kpett.ChatApp.DTOs.Response.Shared;

namespace Kpett.ChatApp.Services.Interfaces
{
    /// <summary>
    /// Service quản lý thông báo: lấy danh sách, đếm chưa đọc, đánh dấu đã đọc.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>Lấy danh sách thông báo của người dùng (cursor pagination).</summary>
        Task<PaginatedData<NotificationResponse>> GetUserNotificationsAsync(string currentUserId, CursorPaginationRequest request, CancellationToken cancel);

        /// <summary>Đếm số thông báo chưa đọc.</summary>
        Task<int> GetUnreadCountAsync(string currentUserId, CancellationToken cancel);

        /// <summary>Đánh dấu một thông báo đã đọc.</summary>
        Task MarkAsReadAsync(string currentUserId, string notificationId, CancellationToken cancel);

        /// <summary>Đánh dấu tất cả thông báo đã đọc.</summary>
        Task MarkAllAsReadAsync(string currentUserId, CancellationToken cancel);
    }
}
