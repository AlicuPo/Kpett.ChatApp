using Azure.Core;
using Kpett.ChatApp.Constants;
using Kpett.ChatApp.DTOs.Request.Auth;
using Kpett.ChatApp.DTOs.Response.Auth;
using Kpett.ChatApp.DTOs.Response.User;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Helpers;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Options;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using UUIDNext;

namespace Kpett.ChatApp.Services.Implementations;

/// <summary>Service xác thực: đăng nhập, đăng ký, đăng xuất, refresh token, quên mật khẩu.</summary>
public class AuthService : IAuthService
{
    private readonly AppDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly EmailOptions _emailOptions;
    private readonly IRedisService _redis;
    private readonly IJwtService _token;
    private readonly ILogger<AuthService> _logger;

    /// <summary>Khởi tạo service với các dependencies.</summary>
    public AuthService(
        AppDbContext context,
        IJwtService token,
        IRedisService redis,
        IEmailService emailService,
        IOptions<EmailOptions> emailOptions, ILogger<AuthService> logger)
    {
        _dbContext = context;
        _token = token;
        _redis = redis;
        _logger = logger;
        _emailService = emailService;
        _emailOptions = emailOptions.Value;
    }

    /// <inheritdoc />
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancel = default)
    {
        _logger.LogInformation("Login attempt received");

        if (string.IsNullOrEmpty(request.Email))
        {
            _logger.LogWarning("Login attempt rejected because email is empty");
            throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Email not empty");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

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
            .FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancel);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
        {
            _logger.LogWarning("Login attempt failed because credentials are invalid");
            throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "Wrong email or password");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Inactive user {UserId} attempted to login", user.Id);
            throw new ForbiddenException(ErrorCodes.USER.INACTIVE, "User inactive");
        }

        var accessToken = _token.GenerateAccessToken(user.Id, user.Email);
        var refreshToken = _token.GenerateRefreshToken(user.Id, user.Email);
        await _redis.SaveRefreshTokenAsync(user.Id, refreshToken, GetTokenRemainingTtl(refreshToken, TimeSpan.FromDays(30)));

        var userRes = new UserResponse()
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            IsVerified = user.IsVerified,
            IsProfileCompleted = !string.IsNullOrEmpty(user.DisplayName) && !string.IsNullOrEmpty(user.Username),
            CreatedAt = user.CreatedAt
        };

        var tokenRes = new TokenResponse()
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer"
        };

        _logger.LogInformation("User {UserId} logged in successfully", user.Id);
        return new LoginResponse
        {
            User = userRes,
            Token = tokenRes
        };
    }

    /// <inheritdoc />
    public async Task<int> RegisterAsync(RegisterRequest request, CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();

        _logger.LogInformation("Register attempt received");
        ValidateAuthRequest(request);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var existingUserByEmail =
            await _dbContext.Users.AnyAsync(x => x.Email == normalizedEmail, cancel);
        if (existingUserByEmail)
        {
            _logger.LogWarning("Register attempt rejected because email already exists");
            throw new ConflictException(ErrorCodes.USER.ALREADY_EXISTS_BY_EMAIL, "Email really existing");
        }

        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var id = Uuid.NewDatabaseFriendly(Database.SqlServer).ToString("N");

        var newUser = new User
        {
            Id = id,
            Password = hashedPassword,
            Email = normalizedEmail,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Status = UserStatusEnums.Offline.GetDescription()
        };

        _dbContext.Users.Add(newUser);
        var result = await _dbContext.SaveChangesAsync(cancel);
        _logger.LogInformation("User {UserId} registered successfully", newUser.Id);
        return result;
    }

    /// <inheritdoc />
    public async Task<bool> LogoutAsync(LogoutRequest logoutRequest, ClaimsPrincipal user, CancellationToken cancel = default)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || string.IsNullOrWhiteSpace(userIdClaim.Value))
        {
            _logger.LogWarning("Logout attempt rejected because user claim is invalid");
            throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "Invalid user");
        }
        var userId = userIdClaim.Value;
        _logger.LogInformation("User {UserId} logout requested", userId);

        var jtiClaim = user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
        var expClaim = user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Exp)?.Value;

        if (!string.IsNullOrEmpty(jtiClaim) && long.TryParse(expClaim, out var expSeconds))
        {
            var expirationTime = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
            var remainTtl = expirationTime - DateTime.UtcNow;

            if (remainTtl > TimeSpan.Zero)
            {
                await _redis.BlacklistAccessTokenAsync(jtiClaim, remainTtl);
                _logger.LogInformation("Access token JTI {Jti} blacklisted for user {UserId}", jtiClaim, userId);
            }
        }

        if (logoutRequest != null && !string.IsNullOrEmpty(logoutRequest.RefreshToken))
        {
            var refreshRemainTtl = TimeSpan.FromDays(30);
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();

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

        await _redis.RemoveRefreshTokenAsync(userId);

        _logger.LogInformation("User {UserId} logged out successfully", userId);
        return true;
    }

    /// <inheritdoc />
    public async Task<TokenResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            _logger.LogWarning("Refresh token request rejected because token is empty");
            throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Refresh token is required");
        }

        var principal = _token.GetPrincipalFromExpiredToken(request.RefreshToken, true);
        if (principal == null)
        {
            _logger.LogWarning("Refresh token request rejected because token principal is invalid");
            throw new UnauthorizedException(ErrorCodes.AUTH.REFRESH_TOKEN_INVALID, "Invalid refresh token");
        }

        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.NameId)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Refresh token request rejected because user ID claim is missing");
            throw new UnauthorizedException(ErrorCodes.AUTH.REFRESH_TOKEN_INVALID, "Invalid refresh token");
        }

        var email = principal.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
        {
            _logger.LogWarning("Refresh token request rejected for user {UserId} because email claim is missing", userId);
            throw new UnauthorizedException(ErrorCodes.AUTH.REFRESH_TOKEN_INVALID, "Invalid refresh token");
        }

        _logger.LogInformation("Refresh token rotation requested for user {UserId}", userId);
        var isBlackList = await _redis.IsRefreshTokenBlacklistedAsync(request.RefreshToken);
        if (isBlackList)
        {
            _logger.LogWarning("Refresh token rotation rejected for user {UserId} because token is blacklisted", userId);
            throw new UnauthorizedException(ErrorCodes.AUTH.REFRESH_TOKEN_INVALID, "Token in black list");
        }

        var storedRefreshToken = await _redis.GetRefreshTokenAsync(userId);
        if (!string.Equals(storedRefreshToken, request.RefreshToken, StringComparison.Ordinal))
        {
            _logger.LogWarning("Refresh token rotation rejected for user {UserId} because token was revoked or rotated", userId);
            throw new UnauthorizedException(ErrorCodes.AUTH.REFRESH_TOKEN_INVALID, "Refresh token has been revoked or rotated");
        }

        var newAccessToken = _token.GenerateAccessToken(userId, email);
        var newRefreshToken = _token.GenerateRefreshToken(userId, email);

        await _redis.BlacklistRefreshTokenAsync(request.RefreshToken, GetTokenRemainingTtl(request.RefreshToken, TimeSpan.FromDays(30)));
        await _redis.SaveRefreshTokenAsync(userId, newRefreshToken, GetTokenRemainingTtl(newRefreshToken, TimeSpan.FromDays(30)));

        _logger.LogInformation("Refresh token rotated successfully for user {UserId}", userId);
        return new TokenResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken
        };
    }

    private static TimeSpan GetTokenRemainingTtl(string token, TimeSpan fallback)
    {
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
        {
            return fallback;
        }

        var jwtToken = handler.ReadJwtToken(token);
        var ttl = jwtToken.ValidTo - DateTime.UtcNow;

        return ttl > TimeSpan.Zero ? ttl : fallback;
    }


    /// <inheritdoc />
    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();

        var normalizedEmail = NormalizeRequiredEmail(request?.Email);
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && u.IsActive, cancel);

        if (user == null)
        {
            return;
        }

        var otpLength = _emailOptions.PasswordResetOtpLength <= 0 ? 6 : _emailOptions.PasswordResetOtpLength;
        var otpLifetime = TimeSpan.FromMinutes(
            _emailOptions.PasswordResetOtpExpiryMinutes <= 0
                ? 10
                : _emailOptions.PasswordResetOtpExpiryMinutes);

        var otp = GenerateNumericOtp(otpLength);

        await _redis.SavePasswordResetOtpAsync(normalizedEmail, otp, otpLifetime);
        await _emailService.SendPasswordResetOtpAsync(user.Email, otp, otpLifetime, cancel);
    }

    /// <inheritdoc />
    public async Task ResetPasswordWithOtpAsync(ResetPasswordWithOtpRequest request, CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();

        var normalizedEmail = NormalizeRequiredEmail(request?.Email);

        if (string.IsNullOrWhiteSpace(request?.Otp))
        {
            throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "OTP is required");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "New password is required");
        }

        var savedOtp = await _redis.GetPasswordResetOtpAsync(normalizedEmail);
        if (string.IsNullOrWhiteSpace(savedOtp) || !string.Equals(savedOtp, request.Otp.Trim(), StringComparison.Ordinal))
        {
            throw new UnauthorizedException(ErrorCodes.AUTH.PASSWORD_RESET_OTP_INVALID, "Invalid or expired OTP");
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail && u.IsActive, cancel);
        if (user == null)
        {
            throw new UnauthorizedException(ErrorCodes.AUTH.PASSWORD_RESET_OTP_INVALID, "Invalid or expired OTP");
        }

        user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancel);
        await _redis.RemovePasswordResetOtpAsync(normalizedEmail);
    }

    private static string NormalizeRequiredEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Email is required");
        }

        return email.Trim().ToLowerInvariant();
    }

    private static string GenerateNumericOtp(int length)
    {
        var upperExclusive = (int)Math.Pow(10, length);
        return RandomNumberGenerator.GetInt32(0, upperExclusive).ToString($"D{length}");
    }

    private static void ValidateAuthRequest(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.Email))
        {
            throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Password or Email is required");
        }
    }
}
