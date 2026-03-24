using Kpett.ChatApp.DTOs.Request.User;
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

        public UsersController(IUserService usersService)
        {
            _userService = usersService;
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
    }
}
