using Kpett.ChatApp.DTOs;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Receive;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly INotificationService _notification;
        public NotificationController(INotificationService notification , AppDbContext dbContext)
        {
            _dbContext = dbContext;
            _notification = notification;
        }

        [HttpPost("Notification")]
        public async Task<IActionResult> CreateMessageNotifications([FromQuery] string conversationId, [FromQuery] string senderId, [FromBody] MessageDTO dto)
        {
            try
            {
                await _notification.CreateMessageNotificationsAsync(conversationId, senderId, dto);
                return Ok(new GeneralResponse
                {
                    
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Notifications created successfully",
                    Return = true


                });
            }
            catch (Exception ex)
            {
                return BadRequest(new GeneralResponse
                {
                    Message = ex.Message,
                    ErorrCode = StatusCodes.Status400BadRequest,
                    Return = false
                });
            }
        }
    }
}
