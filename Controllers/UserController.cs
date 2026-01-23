using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
     
    public class UserController : ControllerBase
    {
        private readonly IUsers _usersRepository;
        public UserController(IUsers usersRepository)
        {
            _usersRepository = usersRepository;
        }

        [HttpPost("get-all-user")]
        [Authorize]
        public async Task<IActionResult> GetAllUser(UserRequest usercurrent,CancellationToken cancel = default)
        {
            var (users, totalCount) = await _usersRepository.GetAllUser(usercurrent, cancel);
            return Ok(new { data = users, totalCount = totalCount });
        }
    }
}
