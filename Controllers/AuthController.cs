using Kpett.ChatApp.DTOs.Request.Auth;
using Kpett.ChatApp.DTOs.Response.Auth;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Abstractions;
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

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancel = default)
        {
            await _authService.ForgotPasswordAsync(request, cancel);

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "If the email exists, an OTP has been sent."
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordWithOtpRequest request, CancellationToken cancel = default)
        {
            await _authService.ResetPasswordWithOtpAsync(request, cancel);

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Password reset successfully."
            });
        }



        [Authorize]
        [HttpPost("revoke")]
        public async Task<IActionResult> Revoke()
        {
            // Trích xuất JTI và exp từ token hiện tại
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

            // Tính TTL chính xác từ exp claim thực tế của token
            // tránh trường hợp token còn nhiều giờ nhưng chỉ blacklist 30 phút
            var expClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Exp)?.Value;
            var ttl = TimeSpan.FromMinutes(15); // Fallback an toàn

            if (!string.IsNullOrEmpty(expClaim) && long.TryParse(expClaim, out long expSeconds))
            {
                var expirationTime = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
                var remaining = expirationTime - DateTime.UtcNow;
                if (remaining > TimeSpan.Zero)
                {
                    ttl = remaining;
                }
            }

            await _redisService.BlacklistAccessTokenAsync(jtiClaim, ttl);

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                Message = "Token revoked successfully.",
                StatusCode = 200
            });
        }
    }
}
