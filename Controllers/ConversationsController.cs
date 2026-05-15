using Kpett.ChatApp.DTOs.Request.Conversation;
using Kpett.ChatApp.DTOs.Request.Friend;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Conversation;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.DTOs.Response.User;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ConversationsController : ControllerBase
    {
        private readonly IConversationService _conversationService;
        private readonly IRelationshipService _relationshipService;

        public ConversationsController(IConversationService conversation, IRelationshipService relationshipService)
        {
            _conversationService = conversation;
            _relationshipService = relationshipService;
        }

        [HttpPost]
        public async Task<ActionResult<ConversationResponse>> CreateConversation([FromBody] CreateConversationRequest request, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();
            var conversation = await _conversationService.CreateConversationAsync(currentUserId, request, cancel);
            return Created($"/api/conversations/{conversation.Id}", new GeneralResponse<ConversationResponse>
            {
                IsSuccess = true,
                Message = "Create conversation successfully",
                Data = conversation,
                StatusCode = StatusCodes.Status201Created
            });
        }

        [HttpGet]
        public async Task<ActionResult<GeneralResponse<PaginatedData<ConversationResponse>>>> GetConversations([FromQuery] ConversationListRequest search, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();
            var conversations = await _conversationService.GetConversationsAsync(currentUserId, search, cancel);
            return Ok(new GeneralResponse<PaginatedData<ConversationResponse>>
            {
                IsSuccess = true,
                Data = conversations,
                Message = "Get conversation successfully",
                StatusCode = StatusCodes.Status200OK
            });

        }

        [HttpGet("has-unread")]
        public async Task<ActionResult<GeneralResponse<bool>>> HasUnread(CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();
            var hasUnread = await _conversationService.HasUnreadConversationAsync(currentUserId, cancel);

            return Ok(new GeneralResponse<bool>
            {
                IsSuccess = true,
                Data = hasUnread,
                Message = "Get conversation unread status successfully",
                StatusCode = StatusCodes.Status200OK
            });
        }

        [HttpGet("{id}/friends-not-in-group")]
        public async Task<ActionResult<GeneralResponse<PaginatedData<UserResponse>>>> GetFriendsNotInGroup([FromRoute] string id, [FromQuery] GetFriendsNotInGroupRequest request, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();
            request.ConversationId = id;
            var result = await _relationshipService.GetFriendsNotInGroupAsync(currentUserId, request, cancel);

            return Ok(new GeneralResponse<PaginatedData<UserResponse>>
            {
                IsSuccess = true,
                Message = "Get friends not in group successfully",
                Data = result,
                StatusCode = StatusCodes.Status200OK
            });
        }

        /// <summary>
        /// POST /api/conversations/conv_123/members
        /// </summary>
        [HttpPost("{id}/members")]
        public async Task<IActionResult> AddMember([FromRoute] string id, [FromBody] AddMembersRequest request, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();

            request.ConversationId = id;

            await _conversationService.AddMembersToGroupAsync(currentUserId, request, cancel);

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                Message = "Added member",
                StatusCode = StatusCodes.Status200OK
            });
        }

        /// <summary>
        /// DELETE /api/conversations/conv_123/members/user_456
        /// </summary>
        [HttpDelete("{id}/members/{userIdToRemove}")]
        public async Task<IActionResult> RemoveMember([FromRoute] string id, [FromRoute] string userIdToRemove, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();

            await _conversationService.RemoveMemberFromGroupAsync(currentUserId, id, userIdToRemove, cancel);

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                Message = "Remove member successfully",
                StatusCode = StatusCodes.Status200OK
            });
        }

        /// <summary>
        /// GET: api/v1/conversations/{conversationId}/messages?limit=20&cursor=xxx
        /// </summary>
        [HttpGet("{conversationId}/messages")]
        public async Task<ActionResult<GeneralResponse<PaginatedData<MessageResponse>>>> GetMessages([FromRoute] string conversationId, [FromQuery] MessageListRequest request, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();

            var response = await _conversationService.GetMessagesAsync(currentUserId, conversationId, request, cancel);

            return Ok(new GeneralResponse<PaginatedData<MessageResponse>>
            {
                IsSuccess = true,
                Data = response,
                Message = "Get messages successfully",
                StatusCode = StatusCodes.Status200OK
            });
        }

        /// <summary>
        /// POST: api/v1/conversations/{conversationId}/messages
        /// </summary>
        [HttpPost("{conversationId}/messages")]
        public async Task<IActionResult> SendMessage([FromRoute] string conversationId, [FromBody] SendMessageRequest request, CancellationToken cancel)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Unauthorized(new { Message = "User is not authenticated." });
            }

            var response = await _conversationService.SendMessageAsync(currentUserId, conversationId, request, cancel);

            return StatusCode(StatusCodes.Status201Created, new GeneralResponse<MessageResponse>
            {
                IsSuccess = true,
                Message = "Send message successfully",
                Data = response,
                StatusCode = StatusCodes.Status201Created
            });
        }

        /// <summary>
        /// </summary>
        [HttpPut("{conversationId}/read")]
        public async Task<IActionResult> MarkAsRead(string conversationId, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();

            await _conversationService.MarkAsReadAsync(conversationId, currentUserId, cancel);

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "Đánh dấu đã đọc thành công"
            });
        }

        /// <summary>
        /// GET: api/conversations/{id}
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<GeneralResponse<ConversationResponse>>> GetConversationById([FromRoute] string id, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();

            var conversation = await _conversationService.GetConversationByIdAsync(currentUserId, id, cancel);

            return Ok(new GeneralResponse<ConversationResponse>
            {
                IsSuccess = true,
                Data = conversation,
                Message = "Get conversation successfully",
                StatusCode = StatusCodes.Status200OK
            });
        }

        /// <summary>
        /// GET: api/conversations/direct/{userId}
        /// Get or create a direct conversation with a specific user.
        /// </summary>
        [HttpGet("direct/{userId}")]
        public async Task<ActionResult<GeneralResponse<ConversationResponse>>> GetOrCreateDirectConversation([FromRoute] string userId, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();

            var conversation = await _conversationService.GetOrCreateDirectConversationAsync(currentUserId, userId, cancel);

            return Ok(new GeneralResponse<ConversationResponse>
            {
                IsSuccess = true,
                Data = conversation,
                Message = "Get or create direct conversation successfully",
                StatusCode = StatusCodes.Status200OK
            });
        }

        [HttpGet("{conversationId}/members")]
        public async Task<IActionResult> GetGroupMembers([FromRoute] string conversationId, [FromQuery] CursorPaginationRequest request, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();

            var paginatedMembers = await _conversationService.GetGroupMembersAsync(currentUserId, conversationId, request, cancel);

            return Ok(new GeneralResponse<PaginatedData<ParticipantResponse>>
            {
                Data = paginatedMembers,
                Message = "Get paticipant successfully",
                StatusCode = 200
            });
        }
    }
}

