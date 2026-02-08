using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConversationsController : ControllerBase
    {
        private readonly IConversationService _conversation;
        public ConversationsController(IConversationService conversation)
        {
            _conversation = conversation;
        }
        [HttpPost("CreateConversations")]
        public async Task<IActionResult> GetConversations([FromQuery] ConversationKeysRequest request, CancellationToken cancel)
        {
            try
            {
                var conversations = await _conversation.CreateConversaTion(request, cancel);
                return Ok(new GeneralResponse<ConversationResponse>
                {
                    Data = conversations,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Tạo cuộc trò chuyện thành công",
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Message = ex.Message,
                    ErorrCode = StatusCodes.Status400BadRequest
                });
            }
        }
    }
}
