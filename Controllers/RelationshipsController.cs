using Kpett.ChatApp.DTOs.Request.Friend;
using Kpett.ChatApp.DTOs.Response.Friend;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.DTOs.Response.User;
using Kpett.ChatApp.Helpers;
using Kpett.ChatApp.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")] 
    [ApiController]
    // [Authorize]
    public class RelationshipsController : ControllerBase
    {
        private readonly IRelationshipService _relationshipService;

        public RelationshipsController(IRelationshipService relationshipService)
        {
            _relationshipService = relationshipService;
        }

        // --- FRIEND REQUESTS ---

        [HttpPost("friend-requests")]
        public async Task<ActionResult> SendFriendRequest([FromBody] SendFriendRequestRequest request)
        {
            var senderId = User.GetRequiredUserId();
            var result = await _relationshipService.SendFriendRequestAsync(senderId, request.ReceiverId);

            return Ok(new GeneralResponse<FriendRequestResponse>
            {
                IsSuccess = true,
                Data = result,
                Message = "Friend request sent successfully",
                StatusCode = StatusCodes.Status201Created
            });
        }

        [HttpPost("friend-requests/{requestId}/accept")]
        public async Task<IActionResult> AcceptFriendRequest(string requestId)
        {
            var currentUserId = User.GetRequiredUserId();

            await _relationshipService.AcceptFriendRequestAsync(currentUserId, requestId);

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                Message = "Friend request accepted successfully",
                StatusCode = StatusCodes.Status200OK
            });
        }

        [HttpPost("friend-requests/{requestId}/decline")]
        public async Task<IActionResult> DeclineFriendRequest(string requestId)
        {
            var currentUserId = User.GetRequiredUserId();
            await _relationshipService.DeclineFriendRequestAsync(currentUserId, requestId);

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                Message = "Friend request declined successfully",
                StatusCode = StatusCodes.Status200OK
            });
        }

        [HttpPost("friend-requests/{requestId}/cancel")]
        public async Task<IActionResult> CancelFriendRequest(string requestId)
        {
            var currentUserId = User.GetRequiredUserId();
            await _relationshipService.CancelFriendRequestAsync(currentUserId, requestId);

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                Message = "Friend request canceled successfully",
                StatusCode = StatusCodes.Status200OK
            });
        }

        // --- FRIENDSHIPS ---

        [HttpGet("friends")]
        public async Task<ActionResult<GeneralResponse<PaginatedData<FriendListItemDTO>>>> GetFriends(
            [FromQuery] FriendListRequest request,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var friends = await _relationshipService.GetFriendsAsync(userId, request, cancel);

            return Ok(new GeneralResponse<PaginatedData<FriendListItemDTO>>
            {
                IsSuccess = true,
                Message = "Get friends successfully",
                StatusCode = StatusCodes.Status200OK,
                Data = friends
            });
        }

        [HttpDelete("{targetUserId}")]
        public async Task<IActionResult> Unfriend(string targetUserId)
        {
            var currentUserId = User.GetRequiredUserId();
            await _relationshipService.UnfriendAsync(currentUserId, targetUserId);

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                Message = "Unfriended successfully",
                StatusCode = StatusCodes.Status200OK
            });
        }

        [HttpPost("follows/{followeeId}")]
        public async Task<IActionResult> Follow(string followeeId)
        {
            var currentUserId = User.GetRequiredUserId();
            await _relationshipService.FollowAsync(currentUserId, followeeId);

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                Message = "Followed successfully",
                StatusCode = StatusCodes.Status200OK
            });
        }

        [HttpDelete("follows/{followeeId}")]
        public async Task<IActionResult> Unfollow(string followeeId)
        {
            var currentUserId = User.GetRequiredUserId();
            await _relationshipService.UnfollowAsync(currentUserId, followeeId);

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                Message = "Unfollowed successfully",
                StatusCode = StatusCodes.Status200OK
            });
        }

        [HttpGet("friends/suggestions")]
        public async Task<IActionResult> GetFriendSuggestions([FromQuery] int limit = 10, CancellationToken cancel = default)
        {
            var currentUserId = User.GetRequiredUserId();
            var suggestions = await _relationshipService.GetFriendSuggestionsAsync(currentUserId, limit, cancel);

            return Ok(new GeneralResponse<List<UserResponse>>
            {
                IsSuccess = true,
                Message = "Get friend suggestions successfully",
                Data = suggestions,
                StatusCode = StatusCodes.Status200OK
            });
        }
    }
}

