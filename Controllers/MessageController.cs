using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MessageController : ControllerBase
    {
        private readonly IMessage _message;
        private readonly IToken _Itoken;
        public MessageController(IMessage message)
        {
            _message = message;

        }
        [HttpPost("SendMessage")]
        public async Task<IActionResult> SendMessage(string senderId, MessageRequest request)
        {
            await _message.SendMessageAsync(senderId, request);
            return Ok();
        }

        [HttpGet("GetUserConversations")]
        public async Task<IActionResult> GetUserConversations(string currentUserId)
        {
            var conversations = await _message.GetUserConversations(currentUserId);
            return Ok(conversations);
        }

        [Authorize]
        [HttpPost("GetOrCreateOneToOneConversation")]
        public async Task<IActionResult> GetOrCreate([FromBody] CreateOneToOneChatRequest request)
        {
            // 1. Lấy trực tiếp từ User.Claims để kiểm tra
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("sub")?.Value; // "sub" là tên gốc trong JWT

            if (string.IsNullOrEmpty(userIdClaim))
                return BadRequest("Lỗi: Token hợp lệ nhưng không tìm thấy ID người dùng bên trong!");

            var conversationId = await _message.GetOrCreateOneToOneConversation(userIdClaim, request.TargetUserId);
            return Ok(new { conversationId });
        }

    }
}
