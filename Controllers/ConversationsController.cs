using Kpett.ChatApp.DTOs.Request.Conversation;
using Kpett.ChatApp.DTOs.Request.Firend;
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
            return Created($"/api/conversations/{conversation.Id}", conversation);
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
        /// Thêm 1 người vào nhóm chat
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
        /// Xóa 1 người khỏi nhóm, hoặc tự rời nhóm
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
        /// Lấy danh sách tin nhắn của một cuộc hội thoại (Phân trang ngược)
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
        /// Gửi một tin nhắn mới vào cuộc hội thoại (Hỗ trợ text, ảnh, video, file đính kèm)
        /// POST: api/v1/conversations/{conversationId}/messages
        /// </summary>
        /// <param name="conversationId">ID của cuộc hội thoại (Lấy từ URL)</param>
        /// <param name="request">Payload chứa ClientMessageId, Nội dung và mảng Attachments</param>
        /// <param name="cancel">Cancellation Token</param>
        /// <returns>Đối tượng MessageResponse vừa được tạo hoặc đã tồn tại</returns>
        [HttpPost("{conversationId}/messages")]
        public async Task<IActionResult> SendMessage([FromRoute] string conversationId, [FromBody] SendMessageRequest request, CancellationToken cancel)
        {
            // Lấy ID của người dùng đang gọi API từ JWT Token
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Unauthorized(new { Message = "User is not authenticated." });
            }

            // Gọi xuống tầng Service để xử lý logic (Lưu DB, kiểm tra trùng lặp, đẩy SignalR)
            var response = await _conversationService.SendMessageAsync(currentUserId, conversationId, request, cancel);

            // Trả về mã trạng thái HTTP 201 (Created) kèm theo toàn bộ dữ liệu tin nhắn (MessageResponse)
            // Frontend sẽ nhận cục JSON này để cập nhật UI nếu cần (hoặc đợi qua SignalR)
            return StatusCode(StatusCodes.Status201Created, new GeneralResponse<MessageResponse>
            {
                IsSuccess = true,
                Message = "Send message successfully",
                Data = response,
                StatusCode = StatusCodes.Status201Created
            });
        }

        /// <summary>
        /// Đánh dấu toàn bộ tin nhắn trong phòng là đã đọc
        /// </summary>
        [HttpPut("{conversationId}/read")]
        public async Task<IActionResult> MarkAsRead(string conversationId, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();

            // ĐỔI TỪ _messageService SANG _conversationService
            await _conversationService.MarkAsReadAsync(conversationId, currentUserId, cancel);

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "Đánh dấu đã đọc thành công"
            });
        }

        /// <summary>
        /// Lấy thông tin chi tiết của một nhóm chat/cuộc hội thoại
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
    }
}
