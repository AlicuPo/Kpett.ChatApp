using CloudinaryDotNet.Actions;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;


namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FriendshipsController : ControllerBase
    {
        private readonly IFriendshipsService _friendshipsServices;
        public FriendshipsController(IFriendshipsService friendshipsServices)
        {
            _friendshipsServices = friendshipsServices;
        }

        [HttpPost("FriendRequest")]
        public async Task<IActionResult> RequestFriendRequest([FromQuery] string senderId, [FromQuery] string receiverId, CancellationToken cancel)
        {
            try
            {
                await _friendshipsServices.RequestFriendRequestAsync(senderId, receiverId, cancel);
                return Ok(new 
                {
                    StatusCode = StatusCode(StatusCodes.Status200OK),
                    Return = true,
                    Message = "gửi lời mời thành công"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new 
                {
                    ErrorCode = StatusCode(StatusCodes.Status400BadRequest),
                    Return = false,
                    Message = ex.Message
                });
            }


        }

        //accept friend request
        [HttpPost("AcceptFriendRequest")]
        public async Task<IActionResult> AcceptFriendRequest([FromQuery] string senderId, [FromQuery] string receiverId, CancellationToken cancel)
        {
            try
            {
                await _friendshipsServices.AcceptFriendRequestAsync(senderId, receiverId, cancel);
                return Ok(new 
                {
                    StatusCode = StatusCode(StatusCodes.Status200OK),
                    Return = true,
                    Message = "đã chấp nhận lời mời thành công"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new 
                {
                    ErrorCode = StatusCode(StatusCodes.Status400BadRequest),
                    Return = false,
                    Message = ex.Message
                });
            }

        }
    }
}
