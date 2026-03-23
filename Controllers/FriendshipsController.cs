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
    public class FriendshipsController : ControllerBase
    {
        private readonly IFriendshipService _friendshipsServices;

        public FriendshipsController(IFriendshipService friendshipsServices)
        {
            _friendshipsServices = friendshipsServices;
        }

        [HttpPost("SendFriendRequest")]
        public async Task<IActionResult> SendFriendRequest([FromQuery] string receiverId, CancellationToken cancel)
        {
            if (string.IsNullOrEmpty(receiverId))
            {
                return BadRequest(new
                {
                    errorCode = StatusCodes.Status400BadRequest,
                    return_value = false,
                    message = "Receiver ID is required"
                });
            }

            var senderId = User.GetRequiredUserId();
            await _friendshipsServices.RequestFriendRequestAsync(senderId, receiverId, cancel);

            return Ok(new
            {
                statusCode = StatusCodes.Status200OK,
                return_value = true,
                message = "Friend request sent successfully"
            });
        }

        [HttpPost("AcceptFriendRequest")]
        public async Task<IActionResult> AcceptFriendRequest([FromQuery] string senderId, CancellationToken cancel)
        {
            if (string.IsNullOrEmpty(senderId))
            {
                return BadRequest(new
                {
                    errorCode = StatusCodes.Status400BadRequest,
                    return_value = false,
                    message = "Sender ID is required"
                });
            }

            var receiverId = User.GetRequiredUserId();
            await _friendshipsServices.AcceptFriendRequestAsync(senderId, receiverId, cancel);

            return Ok(new
            {
                statusCode = StatusCodes.Status200OK,
                return_value = true,
                message = "Friend request accepted successfully"
            });
        }

        [HttpPost("RejectFriendRequest")]
        public async Task<IActionResult> RejectFriendRequest([FromQuery] string senderId, CancellationToken cancel)
        {
            if (string.IsNullOrEmpty(senderId))
            {
                return BadRequest(new
                {
                    errorCode = StatusCodes.Status400BadRequest,
                    return_value = false,
                    message = "Sender ID is required"
                });
            }

            var receiverId = User.GetRequiredUserId();
            await _friendshipsServices.RejectFriendRequestAsync(senderId, receiverId, cancel);

            return Ok(new
            {
                statusCode = StatusCodes.Status200OK,
                return_value = true,
                message = "Friend request rejected successfully"
            });
        }

        [HttpPost("CancelFriendRequest")]
        public async Task<IActionResult> CancelFriendRequest([FromQuery] string receiverId, CancellationToken cancel)
        {
            if (string.IsNullOrEmpty(receiverId))
            {
                return BadRequest(new
                {
                    errorCode = StatusCodes.Status400BadRequest,
                    return_value = false,
                    message = "Receiver ID is required"
                });
            }

            var senderId = User.GetRequiredUserId();
            await _friendshipsServices.CancelFriendRequestAsync(senderId, receiverId, cancel);

            return Ok(new
            {
                statusCode = StatusCodes.Status200OK,
                return_value = true,
                message = "Friend request cancelled successfully"
            });
        }

        [HttpGet("PendingFriendRequests")]
        public async Task<IActionResult> GetPendingFriendRequests(CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var requests = await _friendshipsServices.GetPendingFriendRequestsAsync(userId, cancel);

            return Ok(new
            {
                statusCode = StatusCodes.Status200OK,
                return_value = true,
                message = "Pending friend requests retrieved successfully",
                data = requests,
                totalCount = requests.Count
            });
        }
    }
}
