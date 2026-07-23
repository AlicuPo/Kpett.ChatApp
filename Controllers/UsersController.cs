using Kpett.ChatApp.DTOs.Request.Post;
using Kpett.ChatApp.DTOs.Request.User;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.DTOs.Response.User;
using Kpett.ChatApp.Filters;
using Kpett.ChatApp.Helpers;
using Kpett.ChatApp.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<GeneralResponse<UserGeneralInfoResponse>>> GetMyInfo(CancellationToken cancel = default)
        {
            var userId = User.GetRequiredUserId();
            var myInfo = await _userService.GetMyGeneralInfoAsync(userId, cancel);
            return Ok(new GeneralResponse<UserGeneralInfoResponse>
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Get my info successfully",
                IsSuccess = true,
                Data = myInfo
            });
        }

        [HttpPut("me")]
        [Authorize]
        public async Task<ActionResult<UserGeneralInfoResponse>> UpdateUserGeneralInfo([FromBody] UpdateGeneralInfoUserRequest request, CancellationToken cancel = default)
        {
            var currentUserId = User.GetRequiredUserId();
            var result = await _userService.UpdateUserGeneralInfoAsync(currentUserId, request, cancel);
            return Ok(new GeneralResponse<UserGeneralInfoResponse>
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Update user general info successfully",
                IsSuccess = true,
                Data = result
            });
        }

        [HttpPut("me/media")]
        [Authorize]
        public async Task<ActionResult<UserMediaResponse>> UpdateUserMedia([FromBody] MediaRequest media, [FromQuery] string mediaType, CancellationToken cancel = default)
        {
            var currentUserId = User.GetRequiredUserId();
            var result = await _userService.UpdateUserMediaAsync(currentUserId, media, mediaType);
            return Ok(new GeneralResponse<UserMediaResponse>
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Update user media successfully",
                IsSuccess = true,
                Data = result
            });
        }


        [HttpDelete("me/media/primary")]
        [Authorize]
        public async Task<ActionResult<UserMediaResponse>> DeleteUserMedia([FromQuery] string mediaType)
        {
            var currentUserId = User.GetRequiredUserId();
            var result = await _userService.DeleteUserMediaPrimaryAsync(currentUserId, mediaType);
            return Ok(new GeneralResponse<UserMediaResponse>
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Delete user media primary successfully",
                IsSuccess = result,
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(string id, CancellationToken cancel = default)
        {
            var currentUserId = User.GetRequiredUserId();
            var result = await _userService.DeleteUserAsync(id, currentUserId, cancel);

            return Ok(new GeneralResponse<bool>
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Delete user successfully",
                IsSuccess = true,
                Data = result
            });
        }

        [HttpGet("check-username")]
        public async Task<IActionResult> CheckUsername([FromQuery] string username, CancellationToken cancel = default)
        {
            var result = await _userService.CheckExistByUsernameAsync(username, cancel);

            return Ok(new GeneralResponse<UsernameCheckResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Check username successfully",
                Data = result
            });
        }

        [HttpPost("account-setup")]
        [Authorize]
        public async Task<IActionResult> AccountSetup([FromBody] AccountSetupRequest accountSetupRequest, CancellationToken cancel = default)
        {
            var result = await _userService.AccountSetupAsync(User.GetRequiredUserId(), accountSetupRequest, cancel);

            return Ok(new GeneralResponse<UserResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Account setup successfully",
                Data = result
            });
        }

        [HttpGet("me/stats")]
        [Authorize]
        public async Task<ActionResult<UserWithStatResponse>> GetMyStats(CancellationToken cancel = default)
        {
            var result = await _userService.GetUserStatsAsync(User.GetRequiredUserId(), cancel);

            return Ok(new GeneralResponse<UserWithStatResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Get my stats successfully",
                Data = result
            });
        }

        [HttpGet("profile/{username}")]
        [OptionalAuthorize]
        public async Task<IActionResult> GetUserProfile(string username, CancellationToken cancel = default)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _userService.GetUserProfileAsync(username, currentUserId, cancel);

            return Ok(new GeneralResponse<UserProfileResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Get user profile successfully",
                Data = result
            });
        }

        [HttpGet("search")]
        [OptionalAuthorize]
        public async Task<IActionResult> SearchUsers(
            [FromQuery] string keyword,
            [FromQuery] int limit = 20,
            [FromQuery] string? cursor = null,
            CancellationToken cancel = default)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var result = await _userService.SearchUsersAsync(currentUserId, keyword, limit, cursor, cancel);

            return Ok(new GeneralResponse<PaginatedData<UserResponse>>
            {
                IsSuccess = true,
                Data = result,
                Message = "Tìm kiếm người dùng thành công",
                StatusCode = 200
            });
        }
    }
}
