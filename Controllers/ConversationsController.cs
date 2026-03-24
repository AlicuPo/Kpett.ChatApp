using Kpett.ChatApp.DTOs.Request.Conversation;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Conversation;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/conversations")]
    [ApiController]
    [Authorize]
    public class ConversationsController : ControllerBase
    {
        private readonly IConversationService _conversation;

        public ConversationsController(IConversationService conversation)
        {
            _conversation = conversation;
        }

        [HttpPost]
        public async Task<ActionResult<ConversationResponse>> CreateConversation([FromBody] ConversationKeysRequest request, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();
            var conversation = await _conversation.CreateConversationAsync(currentUserId, request, cancel);
            return Created($"/api/conversations/{conversation.Id}", conversation);
        }

        [HttpGet]
        public async Task<ActionResult<List<ConversationResponse>>> GetConversations([FromQuery] SearchRequest search, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();
            var conversations = await _conversation.GetConversationsAsync(currentUserId, search, cancel);
            return Ok(conversations);
        }
    }
}
