using Kpett.ChatApp.DTOs.Request.Message;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MessagesController : ControllerBase
    {
        private readonly IMessageService _message;

        public MessagesController(IMessageService message)
        {
            _message = message;
        }

        [HttpPost("GetMessages")]
        public async Task<IActionResult> GetMessages(string conversationId, long? cursorMessageId, int pageSize, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();
            var result = await _message.GetMessagesAsync(conversationId, currentUserId, cursorMessageId, pageSize, cancel);

            return Ok(new
            {
                StatusCode = StatusCodes.Status200OK,
                Messages = result.Messages,
                OldestMessageId = result.OldestMessageId,
                HasMore = result.HasMore,
            });
        }

        [HttpPost("MarkAsRead/{id}")]
        public async Task<IActionResult> MarkAsRead(string id, [FromBody] ReadMessageRequest request, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();
            await _message.MarkAsRead(id, currentUserId, request, cancel);

            return Ok(new
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Messages marked as read successfully."
            });
        }

        [HttpPost("SendMessage")]
        public async Task<IActionResult> SendMessageAsync(string conversationId, [FromBody] SendMessageRequest request, CancellationToken cancel)
        {
            var senderId = User.GetRequiredUserId();
            await _message.SendMessageAsync(conversationId, senderId, request, cancel);

            return Ok(new
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "gửi tin nhắn thành công"
            });
        }

        [HttpPost("GetMessagesAsync")]
        public async Task<IActionResult> GetMessagesAsync(string conversationId, [FromBody] long? cursorMessageId, [FromQuery] int pageSize, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();
            var result = await _message.GetMessagesAsync(conversationId, currentUserId, cursorMessageId, pageSize, cancel);

            return Ok(new
            {
                StatusCode = StatusCodes.Status200OK,
                Messages = result.Messages,
                OldestMessageId = result.OldestMessageId,
                HasMore = result.HasMore
            });
        }

        [HttpPost("MarkAsReadAsync/{conversationId}")]
        public async Task<IActionResult> MarkAsReadAsync(string conversationId, [FromBody] long request, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            await _message.MarkAsReadAsync(conversationId, userId, request, cancel);

            return Ok(new
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Messages marked as read successfully."
            });
        }
    }
}
