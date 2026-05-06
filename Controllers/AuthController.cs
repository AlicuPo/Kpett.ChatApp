using Kpett.ChatApp.DTOs.Request.Auth;
using Kpett.ChatApp.DTOs.Response.Auth;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IRedisService _redisService;
        private readonly IAuthService _authService;
        public AuthController(IAuthService authService, IRedisService redisService)
        {
            _authService = authService;
            _redisService = redisService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _authService.LoginAsync(request);
            return Ok(new GeneralResponse<LoginResponse>
            {
                StatusCode = 200,
                IsSuccess = true,
                Data = result,
                Message = "Login successfully."
            });
        }


        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancel = default)
        {
            var result = await _authService.RegisterAsync(request, cancel);
            return Ok(new GeneralResponse()
            {
                IsSuccess = true,
                Message = "Register successfully.",
                StatusCode = StatusCodes.Status201Created
            });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest logoutRequest, CancellationToken cancel = default)
        {
            await _authService.LogoutAsync(logoutRequest, User, cancel);

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                Message = "Logout successful.",
                StatusCode = 200
            });
        }

        [HttpPost("refresh")]
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            return Ok(new GeneralResponse<TokenResponse>
            {
                IsSuccess = true,
                StatusCode = 200,
                Data = await _authService.RefreshTokenAsync(request),
                Message = "Token refreshed successfully."
            });
        }

        [Authorize]
        [HttpPost("revoke")]
        public async Task<IActionResult> Revoke()
        {
            // Extract JTI from current token to revoke
            var jtiClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
            if (string.IsNullOrEmpty(jtiClaim))
            {
                return BadRequest(new ErrorResponse
                {
                    IsSuccess = false,
                    Message = "Invalid token.",
                    ErrorCode = "AUTH.INVALID_TOKEN"
                });
            }

            // Blacklist the access token
            await _redisService.BlacklistAccessTokenAsync(jtiClaim, TimeSpan.FromMinutes(30));

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                Message = "Token revoked successfully.",
                StatusCode = 200
            });
        }
    }
}
