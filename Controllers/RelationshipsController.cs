using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Request.Firend;
using Kpett.ChatApp.DTOs.Request.Friend;
using Kpett.ChatApp.DTOs.Response.Friend;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RelationshipsController : ControllerBase
    {
        private readonly IRelationshipService _friendServices;

        public RelationshipsController(IRelationshipService friendshipsServices)
        {
            _friendServices = friendshipsServices;
        }

        // --- FRIEND REQUESTS ---

        [HttpPost("friend-requests")]
        public async Task<ActionResult> SendFriendRequest([FromBody] SendFriendRequestRequest request)
        {
            var senderId = User.GetRequiredUserId();
            var result = await _friendServices.SendFriendRequestAsync(senderId, request.ReceiverId);

            return Ok(new GeneralResponse<FriendRequestResponse>
            {
                IsSuccess = true,
                Data = result,
                Message = "Friend request sent successfully",
                StatusCode = StatusCodes.Status201Created
            });
        }

        [HttpPost("friend-requests/accept")]
        public async Task<IActionResult> AcceptFriendRequest([FromBody] FriendRequestRequest friendAcceptRequest)
        {
            var currentUserId = User.GetRequiredUserId();

            await _friendServices.AcceptFriendRequestAsync(friendAcceptRequest.SenderId, currentUserId);

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                Message = "Friend request accepted successfully",
                StatusCode = StatusCodes.Status200OK
            });
        }

        [HttpPost("friend-requests/decline")]
        public async Task<IActionResult> DeclineFriendRequest([FromBody] FriendRequestRequest declineRequest)
        {
            var currentUserId = User.GetRequiredUserId();
            await _friendServices.DeclineFriendRequestAsync(declineRequest.SenderId, currentUserId);

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                Message = "Friend request declined successfully",
                StatusCode = StatusCodes.Status200OK
            });
        }

        [HttpPost("friend-requests/cancel")]
        public async Task<IActionResult> CancelFriendRequest([FromBody] FriendRequestRequest cancelRequest)
        {
            var currentUserId = User.GetRequiredUserId();
            await _friendServices.CancelFriendRequestAsync(currentUserId, cancelRequest.receiverId);

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                Message = "Friend request canceled successfully",
                StatusCode = StatusCodes.Status200OK
            });
        }

        // --- FRIENDSHIPS ---

        [HttpGet("/fiends")]
        public async Task<ActionResult<GeneralResponse<PaginatedData<FriendListItemDTO>>>> GetFriends(
            [FromQuery] FriendListRequest request,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var friends = await _friendServices.GetFriendsAsync(userId, request, cancel);

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
            await _friendServices.UnfriendAsync(currentUserId, targetUserId);

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
             await _friendServices.FollowAsync(currentUserId, followeeId);

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
            await _friendServices.UnfollowAsync(currentUserId, followeeId);

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                Message = "Unfollowed successfully",
                StatusCode = StatusCodes.Status200OK
            }); 
        }
    }
}