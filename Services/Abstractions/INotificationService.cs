using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Message;
using Kpett.ChatApp.DTOs.Response.Notification;
using Kpett.ChatApp.DTOs.Response.Shared;

namespace Kpett.ChatApp.Services.Abstractions
{
    /// <summary>
    /// Service qu?n l? thông báo: l?y danh sách, ð?m chýa ð?c, ðánh d?u ð? ð?c.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>L?y danh sách thông báo c?a ngý?i dùng (cursor pagination).</summary>
        Task<PaginatedData<NotificationResponse>> GetUserNotificationsAsync(string currentUserId, CursorPaginationRequest request, CancellationToken cancel);

        /// <summary>Ð?m s? thông báo chýa ð?c.</summary>
        Task<int> GetUnreadCountAsync(string currentUserId, CancellationToken cancel);

        /// <summary>Ðánh d?u m?t thông báo ð? ð?c.</summary>
        Task MarkAsReadAsync(string currentUserId, string notificationId, CancellationToken cancel);

        /// <summary>Ðánh d?u t?t c? thông báo ð? ð?c.</summary>
        Task MarkAllAsReadAsync(string currentUserId, CancellationToken cancel);
    }
}


