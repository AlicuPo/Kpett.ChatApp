using Kpett.ChatApp.DTOs.Request.Conversation;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Conversation;
using Kpett.ChatApp.DTOs.Response.Shared;
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
    public class ConversationsController : ControllerBase
    {
        private readonly IConversationService _conversation;

        public ConversationsController(IConversationService conversation)
        {
            _conversation = conversation;
        }

        [HttpPost("CreateConversations")]
        public async Task<IActionResult> CreateConversation([FromBody] ConversationKeysRequest request, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();
            var conversation = await _conversation.CreateConversaTion(currentUserId, request, cancel);

            return Ok(new GeneralResponse<ConversationResponse>
            {
                Data = conversation,
                StatusCode = StatusCodes.Status200OK,
                Message = "Tạo cuộc trò chuyện thành công",
            });
        }

        [HttpGet("GetConversationList")]
        public async Task<IActionResult> GetConversationList([FromQuery] SearchRequest search, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();
            var conversations = await _conversation.GetConversationList(currentUserId, search, cancel);

            return Ok(new GeneralResponse<List<ConversationResponse>>
            {
                Data = conversations,
                StatusCode = StatusCodes.Status200OK,
                Message = "Lấy danh sách cuộc trò chuyện thành công",
            });
        }
    }
}
