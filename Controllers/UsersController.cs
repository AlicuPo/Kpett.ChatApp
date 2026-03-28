using Kpett.ChatApp.DTOs.Request.User;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.DTOs.Response.User;
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
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IPostService _postFeedService;

        public UsersController(IUserService usersService, IPostService postFeedService)
        {
            _userService = usersService;
            _postFeedService = postFeedService;
        }

        [HttpPost("inforUser")]
        public async Task<IActionResult> GetAllUser(UserRequest usercurrent, CancellationToken cancel = default)
        {
            usercurrent.Id ??= User.GetRequiredUserId();
            var inforUser = await _userService.inforUser(usercurrent, cancel);

            return Ok(new GeneralResponse<UserResponse>
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Notifications created successfully",
                IsSuccess = true,
                Data = inforUser
            });
        }

        [HttpGet("GetAllUser")]
        public async Task<IActionResult> getListUsers([FromQuery] UserRequest search, CancellationToken cancel = default)
        {
            var (users, total) = await _userService.GetAllUser(search, cancel);

            return Ok(new GeneralResponse<List<UserResponse>>
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Get total users successfully",
                IsSuccess = true,
                Data = users
            });
        }

        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<ActionResult<UserResponse>> GetUserById(string id, CancellationToken cancel = default)
        {
            var user = await _userService.inforUser(new UserRequest { Id = id }, cancel);
            return Ok(user);
        }

        [HttpPut("UpdateUser/{id}")]
        public async Task<IActionResult> updateUserBy(string id, [FromBody] UpdateUserRequest request, CancellationToken cancel = default)
        {
            var currentUserId = User.GetRequiredUserId();
            var user = await _userService.UpdateUser(id, currentUserId, request, cancel);

            return Ok(new GeneralResponse<UserResponse>
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Update user successfully",
                IsSuccess = true,
                Data = user
            });
        }

        [HttpDelete("DeleteUser/{id}")]
        public async Task<IActionResult> DeleteUser(string id, CancellationToken cancel = default)
        {
            var currentUserId = User.GetRequiredUserId();
            var result = await _userService.DeleteUser(id, currentUserId, cancel);

            return Ok(new GeneralResponse<bool>
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Delete user successfully",
                IsSuccess = true,
                Data = result
            });
        }

        [AllowAnonymous]
        [HttpGet("{userId}/posts")]
        public async Task<ActionResult<List<PostResponseDTO>>> GetUserPosts(string userId, [FromQuery] SearchRequest request, CancellationToken cancel = default)
        {
            var result = await _postFeedService.GetUserPostsAsync(userId, request, cancel);
            return Ok(result);
        }

        [HttpGet("check-username")]
        public async Task<IActionResult> CheckUsername([FromQuery] string username, CancellationToken cancel = default)
        {
            var result = await _userService.CheckExistByUsername(username, cancel);

            return Ok(new GeneralResponse<UsernameCheckResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Check username successfully",
                Data = result
            });
        }

        [HttpPost("account-setup")]
        public async Task<IActionResult> AccountSetup([FromBody] AccountSetupRequest accountSetupRequest, CancellationToken cancel = default)
        {
            var result = await _userService.AccountSetup(User.GetRequiredUserId(), accountSetupRequest, cancel);

            return Ok(new GeneralResponse<UserResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Account setup successfully",
                Data = result
            }); 
        }

        [HttpGet("me/stats")]
        public async Task<IActionResult> GetMyStats(CancellationToken cancel = default)
        {
            var result = await _userService.GetUserStatsAsync(User.GetRequiredUserId(), cancel);

            return Ok(new GeneralResponse<UserStatsResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Get my stats successfully",
                Data = result
            });
        }

        [HttpGet("profile/{username}")]
        public async Task<IActionResult> GetUserProfile(string username, CancellationToken cancel = default)
        {
            var result = await _userService.GetUserProfileAsync(username, User.GetRequiredUserId() ,cancel);

            return Ok(new GeneralResponse<UserProfileResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Get user profile successfully",
                Data = result
            });
        }
    }
}
