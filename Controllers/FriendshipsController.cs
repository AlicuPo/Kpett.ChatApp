using Kpett.ChatApp.DTOs;
using Kpett.ChatApp.DTOs.Response;
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

        /// <summary>
        /// Send a friend request from senderId to receiverId
        /// </summary>
        [HttpPost("SendFriendRequest")]
        public async Task<IActionResult> SendFriendRequest([FromQuery] string senderId, [FromQuery] string receiverId, CancellationToken cancel)
        {
            try
            {
                if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(receiverId))
                {
                    return BadRequest(new
                    {
                        errorCode = StatusCodes.Status400BadRequest,
                        return_value = false,
                        message = "Sender ID and Receiver ID are required"
                    });
                }

                await _friendshipsServices.RequestFriendRequestAsync(senderId, receiverId, cancel);

                return Ok(new
                {
                    statusCode = StatusCodes.Status200OK,
                    return_value = true,
                    message = "Friend request sent successfully"
                });
            }
            catch (AppException appEx)
            {
                return StatusCode(appEx.StatusCode, new
                {
                    errorCode = appEx.StatusCode,
                    return_value = false,
                    message = appEx.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    errorCode = StatusCodes.Status500InternalServerError,
                    return_value = false,
                    message = $"An error occurred: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Accept a friend request
        /// </summary>
        [HttpPost("AcceptFriendRequest")]
        public async Task<IActionResult> AcceptFriendRequest([FromQuery] string senderId, [FromQuery] string receiverId, CancellationToken cancel)
        {
            try
            {
                if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(receiverId))
                {
                    return BadRequest(new
                    {
                        errorCode = StatusCodes.Status400BadRequest,
                        return_value = false,
                        message = "Sender ID and Receiver ID are required"
                    });
                }

                await _friendshipsServices.AcceptFriendRequestAsync(senderId, receiverId, cancel);

                return Ok(new
                {
                    statusCode = StatusCodes.Status200OK,
                    return_value = true,
                    message = "Friend request accepted successfully"
                });
            }
            catch (AppException appEx)
            {
                return StatusCode(appEx.StatusCode, new
                {
                    errorCode = appEx.StatusCode,
                    return_value = false,
                    message = appEx.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    errorCode = StatusCodes.Status500InternalServerError,
                    return_value = false,
                    message = $"An error occurred: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Reject a friend request
        /// </summary>
        [HttpPost("RejectFriendRequest")]
        public async Task<IActionResult> RejectFriendRequest([FromQuery] string senderId, [FromQuery] string receiverId, CancellationToken cancel)
        {
            try
            {
                if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(receiverId))
                {
                    return BadRequest(new
                    {
                        errorCode = StatusCodes.Status400BadRequest,
                        return_value = false,
                        message = "Sender ID and Receiver ID are required"
                    });
                }

                await _friendshipsServices.RejectFriendRequestAsync(senderId, receiverId, cancel);

                return Ok(new
                {
                    statusCode = StatusCodes.Status200OK,
                    return_value = true,
                    message = "Friend request rejected successfully"
                });
            }
            catch (AppException appEx)
            {
                return StatusCode(appEx.StatusCode, new
                {
                    errorCode = appEx.StatusCode,
                    return_value = false,
                    message = appEx.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    errorCode = StatusCodes.Status500InternalServerError,
                    return_value = false,
                    message = $"An error occurred: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Cancel a sent friend request
        /// </summary>
        [HttpPost("CancelFriendRequest")]
        public async Task<IActionResult> CancelFriendRequest([FromQuery] string senderId, [FromQuery] string receiverId, CancellationToken cancel)
        {
            try
            {
                if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(receiverId))
                {
                    return BadRequest(new
                    {
                        errorCode = StatusCodes.Status400BadRequest,
                        return_value = false,
                        message = "Sender ID and Receiver ID are required"
                    });
                }

                await _friendshipsServices.CancelFriendRequestAsync(senderId, receiverId, cancel);

                return Ok(new
                {
                    statusCode = StatusCodes.Status200OK,
                    return_value = true,
                    message = "Friend request cancelled successfully"
                });
            }
            catch (AppException appEx)
            {
                return StatusCode(appEx.StatusCode, new
                {
                    errorCode = appEx.StatusCode,
                    return_value = false,
                    message = appEx.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    errorCode = StatusCodes.Status500InternalServerError,
                    return_value = false,
                    message = $"An error occurred: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Get all pending friend requests for a user
        /// </summary>
        [HttpGet("PendingFriendRequests")]
        public async Task<IActionResult> GetPendingFriendRequests([FromQuery] string userId, CancellationToken cancel)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest(new
                    {
                        errorCode = StatusCodes.Status400BadRequest,
                        return_value = false,
                        message = "User ID is required"
                    });
                }

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
            catch (AppException appEx)
            {
                return StatusCode(appEx.StatusCode, new
                {
                    errorCode = appEx.StatusCode,
                    return_value = false,
                    message = appEx.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    errorCode = StatusCodes.Status500InternalServerError,
                    return_value = false,
                    message = $"An error occurred: {ex.Message}"
                });
            }
        }
    }
}
