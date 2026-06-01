using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Message;
using Kpett.ChatApp.DTOs.Response.Notidication;
using Kpett.ChatApp.DTOs.Response.Shared;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface INotificationService
    {
        Task<PaginatedData<NotificationResponse>> GetUserNotificationsAsync(string currentUserId, CursorPaginationRequest request, CancellationToken cancel);
        Task<int> GetUnreadCountAsync(string currentUserId, CancellationToken cancel);
        Task MarkAsReadAsync(string currentUserId, string notificationId, CancellationToken cancel);
        Task MarkAllAsReadAsync(string currentUserId, CancellationToken cancel);
    }
}

