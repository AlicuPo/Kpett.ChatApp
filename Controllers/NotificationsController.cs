using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Notification;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Helpers;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] CursorPaginationRequest request, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();
            var data = await _notificationService.GetUserNotificationsAsync(currentUserId, request, cancel);

            return Ok(new GeneralResponse<PaginatedData<NotificationResponse>>
            {
                IsSuccess = true,
                Data = data,
                Message = "L?y danh sách thông báo thành công",
                StatusCode = 200
            });
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount(CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();
            var count = await _notificationService.GetUnreadCountAsync(currentUserId, cancel);

            return Ok(new GeneralResponse<int>
            {
                IsSuccess = true,
                Data = count,
                Message = "L?y s? lý?ng thông báo chýa ð?c thành công",
                StatusCode = 200
            });
        }

        [HttpPut("{notificationId}/read")]
        public async Task<IActionResult> MarkAsRead([FromRoute] string notificationId, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();
            await _notificationService.MarkAsReadAsync(currentUserId, notificationId, cancel);

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                Message = "Ð? ðánh d?u thông báo là ð? ð?c",
                StatusCode = 200
            });
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead(CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();
            await _notificationService.MarkAllAsReadAsync(currentUserId, cancel);

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                Message = "Ð? ðánh d?u t?t c? thông báo là ð? ð?c",
                StatusCode = 200
            });
        }
    }
}