using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using IRedisService = Kpett.ChatApp.Services.Interfaces.IRedisService;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IJwtService _token;
        private readonly IRedisService _redis;
        private readonly IAuthService _loginRepository;
        private readonly AppDbContext _dbContext;
        public AuthController(IAuthService loginRepository, IRedisService redis, IJwtService token, AppDbContext dbContext)
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

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout(CancellationToken cancel = default)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || string.IsNullOrWhiteSpace(userIdClaim.Value))
                {
                    return Unauthorized(new { message = "Invalid user." });
                }
                var userId = userIdClaim.Value;

                // Extract JTI from current access token to blacklist it
                var jtiClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
                if (!string.IsNullOrEmpty(jtiClaim))
                {
                    // Blacklist the current access token (30 min expiry)
                    await _redis.BlacklistAccessTokenAsync(jtiClaim, TimeSpan.FromMinutes(30));
                }

                // Blacklist refresh token
                var refreshToken = await _redis.GetRefreshTokenAsync(userId);
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    await _redis.BlacklistRefreshTokenAsync(refreshToken, TimeSpan.FromDays(30));
                    await _redis.RemoveRefreshTokenAsync(userId);
                }

                var result = await _loginRepository.LogoutAsync(userId, cancel);
                if (result)
                {
                    return Ok(new GeneralResponse
                    {
                        Return = true,
                        Message = "Logout successful.",
                        StatusCode = 200
                    });
                }
                else
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new GeneralResponse
                    {
                        Return = false,
                        Message = "Logout failed.",
                        ErorrCode = 400
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new GeneralResponse
                {
                    Return = false,
                    Message = ex.Message,
                    ErorrCode = 500
                });
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.RefreshToken))
                {
                    return BadRequest(new GeneralResponse
                    {
                        Return = false,
                        Message = "Refresh token is required.",
                        ErorrCode = 400
                    });
                }

                // Validate refresh token signature and extract claims
                var principal = _token.GetPrincipalFromExpiredToken(request.RefreshToken, true);
                if (principal == null)
                {
                    return Unauthorized(new GeneralResponse
                    {
                        Return = false,
                        Message = "Invalid refresh token.",
                        ErorrCode = 401
                    });
                }

                var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                             ?? principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.NameId)?.Value;

                var username = principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Name)?.Value
                               ?? principal.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;

                var email = principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new GeneralResponse
                    {
                        Return = false,
                        Message = "Invalid token claims.",
                        ErorrCode = 401
                    });
                }

                // Check blacklist
                if (await _redis.IsRefreshTokenBlacklistedAsync(request.RefreshToken))
                {
                    return Unauthorized(new GeneralResponse
                    {
                        Return = false,
                        Message = "Refresh token revoked.",
                        ErorrCode = 401
                    });
                }

                // Ensure refresh token matches stored value
                var saved = await _redis.GetRefreshTokenAsync(userId);
                if (string.IsNullOrEmpty(saved) || saved != request.RefreshToken)
                {
                    return Unauthorized(new GeneralResponse
                    {
                        Return = false,
                        Message = "Refresh token does not match.",
                        ErorrCode = 401
                    });
                }

                // Generate new tokens
                var newAccessToken = _token.GenerateAccessToken(userId, username, email);
                var newRefreshToken = _token.GenerateRefreshToken(userId, username, email);

                // Blacklist old refresh token and save new one
                await _redis.BlacklistRefreshTokenAsync(request.RefreshToken, TimeSpan.FromDays(30));
                await _redis.SaveRefreshTokenAsync(userId, newRefreshToken, TimeSpan.FromDays(30));

                var response = new TokenResponse
                {
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshToken,
                    ExpiresIn = 30 * 60,
                    IssuedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30)
                };

                return Ok(new GeneralResponse<TokenResponse>
                {
                    StatusCode = 200,
                    Return = true,
                    Data = response,
                    Message = "Token refreshed successfully."
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

        [Authorize]
        [HttpPost("revoke")]
        public async Task<IActionResult> Revoke()
        {
            try
            {
                // Extract JTI from current token to revoke
                var jtiClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
                if (string.IsNullOrEmpty(jtiClaim))
                {
                    return BadRequest(new GeneralResponse
                    {
                        Return = false,
                        Message = "Invalid token.",
                        ErorrCode = 400
                    });
                }

                // Blacklist the access token
                await _redis.BlacklistAccessTokenAsync(jtiClaim, TimeSpan.FromMinutes(30));

                return Ok(new GeneralResponse
                {
                    Return = true,
                    Message = "Token revoked successfully.",
                    StatusCode = 200
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new GeneralResponse
                {
                    Return = false,
                    Message = ex.Message,
                    ErorrCode = 500
                });
            }
        }
    }
}
