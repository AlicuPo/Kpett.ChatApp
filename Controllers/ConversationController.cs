using Kpett.ChatApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConversationController : ControllerBase
    {
        private readonly IConversation _conversation;
        public ConversationController(IConversation conversation)
        {
            _conversation = conversation;
        }
        [HttpGet("GetConversations")]
        public async  Task<IActionResult> GetConversations([FromQuery] DTOs.Request.SearchRequest search, CancellationToken cancel)
        {
            try
            {
                var conversations = await _conversation.GetConversationList(search, cancel);
                return Ok(new
                {
                    StatusCode = StatusCodes.Status200OK,
                    Conversations = conversations
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
