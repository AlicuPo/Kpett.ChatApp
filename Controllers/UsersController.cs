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
        public async Task<IActionResult> GetAllUser(UserRequest usercurrent,CancellationToken cancel = default)
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

    }
}
