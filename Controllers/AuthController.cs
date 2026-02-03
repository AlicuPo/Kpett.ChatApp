using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using IRedis = Kpett.ChatApp.Services.IRedis;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IToken _token;
        private readonly IRedis _redis;
        private readonly ILogin _loginRepository;
        private readonly AppDbContext _dbContext;
        public AuthController(ILogin loginRepository, IRedis redis, IToken token, AppDbContext dbContext)
        {
            _loginRepository = loginRepository;
            _redis = redis;
            _token = token;
            _dbContext = dbContext;

        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var result = await _loginRepository.LoginAsync(request);
                return Ok(new GeneralResponse<LoginResponse>
                {
                    StatusCode = 200,
                    Return = true,
                    Data = result,
                    Message = "Đăng nhập thành công."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new GeneralResponse
                {
                    Return = false,
                    Message = ex.Message,
                    ErorrCode = 400
                });
            }
        }


        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancel = default)
        {
            try
            {
                var result = await _loginRepository.RegisterAsync(request, cancel);
                return Ok(new
                {
                    Return = true,
                    message = "Đăng ký tài khoản thành công.",
                    StatusCode = StatusCode(StatusCodes.Status201Created)
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new GeneralResponse
                {
                    Return = false,
                    Message = ex.Message,
                    ErorrCode = 400
                });
            }
         
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout(CancellationToken cancel = default)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || string.IsNullOrWhiteSpace(userIdClaim.Value))
            {
                return Unauthorized(new { message = "Invalid user." });
            }
            var userId = userIdClaim.Value;
            var result = await _loginRepository.LogoutAsync(userId, cancel);
            if (result)
            {
                return Ok(new
                {
                    Return = true,
                    Message = "Logout successful.",
                    StatusCode = 200
                });
            }
            else
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { message = "Logout failed." });
            }
        }

    }
}
