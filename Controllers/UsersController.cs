using Kpett.ChatApp.DTOs.Request;
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
    public class UsersController : ControllerBase
    {
        private readonly IUserService _usersRepository;

        public UsersController(IUserService usersRepository)
        {
            _usersRepository = usersRepository;
        }

        [HttpPost("inforUser")]
        public async Task<IActionResult> GetAllUser(UserRequest usercurrent, CancellationToken cancel = default)
        {
            usercurrent.Id ??= User.GetRequiredUserId();
            var inforUser = await _usersRepository.inforUser(usercurrent, cancel);

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
            var (users, total) = await _usersRepository.GetAllUser(search, cancel);

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
            var user = await _usersRepository.UpdateUser(id, currentUserId, request, cancel);

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
            var result = await _usersRepository.DeleteUser(id, currentUserId, cancel);

            return Ok(new GeneralResponse<bool>
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Delete user successfully",
                IsSuccess = true,
                Data = result
            });
        }
    }
}
