using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Request.Friend;
using Kpett.ChatApp.DTOs.Response.Friend;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/friend-requests")]
    [ApiController]
    [Authorize]
    public class FriendshipsController : ControllerBase
    {
        private readonly IFriendshipService _friendshipsServices;

        public FriendshipsController(IFriendshipService friendshipsServices)
        {
            _friendshipsServices = friendshipsServices;
        }

        [HttpPost]
        public async Task<ActionResult<FriendRequestDTO>> CreateFriendRequest([FromBody] CreateFriendRequestRequest request, CancellationToken cancel)
        {
            var senderId = User.GetRequiredUserId();
            var result = await _friendshipsServices.CreateFriendRequestAsync(senderId, request?.ReceiverId ?? string.Empty, cancel);
            return Created($"/api/friend-requests/{result.FriendRequestId}", result);
        }

        [HttpGet]
        public async Task<ActionResult<List<FriendRequestDTO>>> GetPendingFriendRequests([FromQuery] string? status, CancellationToken cancel)
        {
            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Only pending friend requests are supported.");
            }

            var userId = User.GetRequiredUserId();
            var requests = await _friendshipsServices.GetPendingFriendRequestsAsync(userId, cancel);
            return Ok(requests);
        }

        [HttpPatch("{friendRequestId}")]
        public async Task<ActionResult<FriendRequestDTO>> UpdateFriendRequestStatus(
            string friendRequestId,
            [FromBody] UpdateFriendRequestStatusRequest request,
            CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();
            var result = await _friendshipsServices.UpdateFriendRequestStatusAsync(friendRequestId, currentUserId, request?.Status ?? string.Empty, cancel);
            return Ok(result);
        }

        [HttpDelete("{friendRequestId}")]
        public async Task<IActionResult> CancelFriendRequest(string friendRequestId, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();
            await _friendshipsServices.CancelFriendRequestAsync(friendRequestId, currentUserId, cancel);
            return NoContent();
        }
    }
}
