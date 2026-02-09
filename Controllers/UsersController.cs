using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]

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
            try
            {
                var inforUser = await _usersRepository.inforUser(usercurrent, cancel);
                return Ok(new GeneralResponse<UserResponse>
                {
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Notifications created successfully",
                    Return = true,
                    Data = inforUser


                });
            }
            catch (Exception ex)
            {
                return BadRequest(new GeneralResponse
                {
                    Message = ex.Message,
                    ErorrCode = StatusCodes.Status400BadRequest,
                    Return = false
                });
            }
        }
        [HttpGet("GetAllUser")]

        public async Task<IActionResult> getListUsers([FromQuery] UserRequest search, CancellationToken cancel = default)
        {
            try
            {
                var (users, total) = await _usersRepository.GetAllUser(search, cancel);
                return Ok(new GeneralResponse<List<UserResponse>>
                {
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Get total users successfully",
                    Return = true,
                    Data = users
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new GeneralResponse
                {
                    Message = ex.Message,
                    ErorrCode = StatusCodes.Status400BadRequest,
                    Return = false
                });
            }
        }


        [HttpPut("UpdateUser/{id}")]
        public async Task<IActionResult> updateUserBy(string id, [FromBody] UpdateUserRequest request, CancellationToken cancel = default)
        {
            try
            {
                var user = await _usersRepository.UpdateUser(id, request, cancel);
                return Ok(new GeneralResponse<UserResponse>
                {
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Update user successfully",
                    Return = true,
                    Data = user
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new GeneralResponse
                {
                    Message = ex.Message,
                    ErorrCode = StatusCodes.Status400BadRequest,
                    Return = false
                });
            }
        }

        [HttpDelete("DeleteUser/{id}")]
        public async Task<IActionResult> DeleteUser(string id, CancellationToken cancel = default)
        {
            try
            {
                var result = await _usersRepository.DeleteUser(id, cancel);
                return Ok(new GeneralResponse<bool>
                {
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Delete user successfully",
                    Return = true,
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new GeneralResponse
                {
                    Message = ex.Message,
                    ErorrCode = StatusCodes.Status400BadRequest,
                    Return = false
                });
            }
        }

    }
}

