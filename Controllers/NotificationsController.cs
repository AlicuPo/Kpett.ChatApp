using Kpett.ChatApp.DTOs;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Receive;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notification;

        public NotificationsController(INotificationService notification)
        {
            _notification = notification;
        }

        [HttpPost("Notification")]
        public async Task<IActionResult> CreateMessageNotifications([FromQuery] string conversationId, [FromBody] MessageDTO dto)
        {
            var senderId = User.GetRequiredUserId();
            await _notification.CreateMessageNotificationsAsync(conversationId, senderId, dto);

            return Ok(new GeneralResponse
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Notifications created successfully",
                IsSuccess = true
            });
        }
    }
}
