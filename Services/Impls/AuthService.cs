using Azure.Core;
using Kpett.ChatApp.Constants;
using Kpett.ChatApp.DTOs.Request.Auth;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.DTOs.Response.Auth;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.DTOs.Response.User;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using UUIDNext;

namespace Kpett.ChatApp.Services.Impls;

public class AuthService : IAuthService
{
    private readonly ICloudinaryService _cloudinary;

    private readonly AppDbContext _dbContext;
    private readonly IRedisService _redis;
    private readonly IJwtService _token;

    public AuthService(AppDbContext context, IJwtService token, IRedisService redis, ICloudinaryService cloudinary)
    {
        _dbContext = context;
        _token = token;
        _redis = redis;
        _cloudinary = cloudinary;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancel = default)
    {
        if (string.IsNullOrEmpty(request.Email))
        {
            throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Email not empty");
        }

        var user = await _dbContext.Users
            .Select(u => new
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                DisplayName = u.DisplayName,
                Password = u.Password,
                AvatarUrl = _dbContext.UserMedias
                    .Where(um => um.UserId == u.Id && um.IsPrimary && um.MediaType == UserMediaType.Avatar.GetDescription())
                    .Select(um => um.MediaUrl)
                    .FirstOrDefault(),
                IsActive = u.IsActive,
                IsVerified = u.IsVerified,
                CreatedAt = u.CreatedAt
            })
            .FirstOrDefaultAsync(x => x.Email == request.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
        {
            throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "Wrong email or password");
        }

        if (!user.IsActive)
        {
            throw new ForbiddenException(ErrorCodes.USER.INACTIVE, "User inactive");
        }

        // Táº¡o JWT Token
        var accessToken = _token.GenerateAccessToken(user.Id, user.Email);
        var refreshToken = _token.GenerateRefreshToken(user.Id, user.Email);

        var userRes = new UserResponse()
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            IsVerified = user.IsVerified,
            IsProfileCompleted = !string.IsNullOrEmpty(user.DisplayName) && !string.IsNullOrEmpty(user.Username),
            CreatedAt = user.CreatedAt // Dùng CreatedAt thực tế từ DB, không phải thời điểm đăng nhập
        };

        var tokenRes = new TokenResponse()
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer"
        };

        return new LoginResponse
        {
            User = userRes,
            Token = tokenRes
        };
    }

    public async Task<int> RegisterAsync(RegisterRequest request, CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();

        ValidateAuthRequest(request);

        var existingUserByEmail =
            await _dbContext.Users.AnyAsync(x => x.Email == request.Email);
        if (existingUserByEmail)
        {
            throw new ConflictException(ErrorCodes.USER.ALREADY_EXISTS_BY_EMAIL, "Email really existing");
        }

        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var avatarUrl = string.Empty;

        var _id = Uuid.NewDatabaseFriendly(Database.SqlServer).ToString("N");

        var newUser = new User
        {
            Id = _id,
            Password = hashedPassword,
            Email = request.Email,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Status = UserStatusEnums.Offline.GetDescription()
        };
        _dbContext.Users.Add(newUser);
        var result = await _dbContext.SaveChangesAsync(cancel);
        return result;
    }

    public async Task<bool> LogoutAsync(LogoutRequest logoutRequest, ClaimsPrincipal user, CancellationToken cancel = default)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || string.IsNullOrWhiteSpace(userIdClaim.Value))
        {
            throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "Invalid user");
        }
        var userId = userIdClaim.Value;

        // THU Há»’I ACCESS TOKEN (Láº¥y tá»« Header Authorization)
        var jtiClaim = user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
        var expClaim = user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Exp)?.Value;

        if (!string.IsNullOrEmpty(jtiClaim) && long.TryParse(expClaim, out long expSeconds))
        {
            var expirationTime = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
            var remainTtl = expirationTime - DateTime.UtcNow;

            if (remainTtl > TimeSpan.Zero)
            {
                await _redis.BlacklistAccessTokenAsync(jtiClaim, remainTtl);
            }
        }

        // THU Há»’I REFRESH TOKEN
        if (logoutRequest != null && !string.IsNullOrEmpty(logoutRequest.RefreshToken))
        {
            TimeSpan refreshRemainTtl = TimeSpan.FromDays(30);

            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();

            // Kiá»ƒm tra xem token client gá»­i lÃªn cÃ³ pháº£i JWT há»£p lá»‡ khÃ´ng
            if (handler.CanReadToken(logoutRequest.RefreshToken))
            {
                var jwtToken = handler.ReadJwtToken(logoutRequest.RefreshToken);
                var calculatedTtl = jwtToken.ValidTo - DateTime.UtcNow;

                if (calculatedTtl > TimeSpan.Zero)
                {
                    refreshRemainTtl = calculatedTtl;
                }
            }

            await _redis.BlacklistRefreshTokenAsync(logoutRequest.RefreshToken, refreshRemainTtl);
        }

        return true;
    }

    private void ValidateAuthRequest(RegisterRequest request)
    {
        if (string.IsNullOrEmpty(request.Password) || string.IsNullOrEmpty(request.Email))
        {
            throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Password or Email is required");
        }
    }

    public async Task<TokenResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Refresh token is required");
        }

        var principal = _token.GetPrincipalFromExpiredToken(request.RefreshToken, true);
        if (principal == null)
        {
            throw new UnauthorizedException(ErrorCodes.AUTH.REFRESH_TOKEN_INVALID, "Invalid refresh token");
        }

        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.NameId)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedException(ErrorCodes.AUTH.REFRESH_TOKEN_INVALID, "Invalid refresh token");
        }

        var email = principal.FindFirst(ClaimTypes.Email)?.Value;

        if (string.IsNullOrEmpty(email))
        {
            throw new UnauthorizedException(ErrorCodes.AUTH.REFRESH_TOKEN_INVALID, "Invalid refresh token");
        }

        var isBlackList = await _redis.IsRefreshTokenBlacklistedAsync(request.RefreshToken);
        if (isBlackList)
        {
            throw new UnauthorizedException(ErrorCodes.AUTH.REFRESH_TOKEN_INVALID, "Token in black list");
        }

        var newAccessToken = _token.GenerateAccessToken(userId, email);

        return new TokenResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = request.RefreshToken
        };
    }
}
