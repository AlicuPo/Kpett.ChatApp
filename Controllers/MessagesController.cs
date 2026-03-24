using Kpett.ChatApp.DTOs.Request.Message;
using Kpett.ChatApp.DTOs.Response.Message;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/conversations/{conversationId}")]
    [ApiController]
    [Authorize]
    public class MessagesController : ControllerBase
    {
        private readonly IMessageService _message;

        public MessagesController(IMessageService message)
        {
            _message = message;
        }

        [HttpGet("messages")]
        public async Task<ActionResult<MessagePageResult>> GetMessages(
            string conversationId,
            [FromQuery] long? cursorMessageId,
            [FromQuery] int pageSize = 40,
            CancellationToken cancel = default)
        {
            var currentUserId = User.GetRequiredUserId();
            var result = await _message.GetMessagesAsync(conversationId, currentUserId, cursorMessageId, pageSize, cancel);
            return Ok(result);
        }

        [HttpPost("messages")]
        public async Task<ActionResult<MessageDTO>> SendMessage(
            string conversationId,
            [FromBody] SendMessageRequest request,
            CancellationToken cancel)
        {
            var senderId = User.GetRequiredUserId();
            var result = await _message.SendMessageAsync(conversationId, senderId, request, cancel);
            return Created($"/api/conversations/{conversationId}/messages/{result.Id}", result);
        }

        [HttpPut("participants/me/read-state")]
        public async Task<IActionResult> MarkAsRead(string conversationId, [FromBody] ReadMessageRequest request, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();
            await _message.MarkAsReadAsync(conversationId, currentUserId, request, cancel);
            return NoContent();
        }
    }
}
