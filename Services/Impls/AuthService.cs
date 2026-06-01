using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Request.Auth;
using Kpett.ChatApp.DTOs.Response.Auth;
using Kpett.ChatApp.DTOs.Response.User;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Options;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using UUIDNext;

namespace Kpett.ChatApp.Services.Impls;

public class AuthService : IAuthService
{
    private readonly AppDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly EmailOptions _emailOptions;
    private readonly IRedisService _redis;
    private readonly IJwtService _token;

    public AuthService(
        AppDbContext context,
        IJwtService token,
        IRedisService redis,
        IEmailService emailService,
        IOptions<EmailOptions> emailOptions)
    {
        _dbContext = context;
        _token = token;
        _redis = redis;
        _emailService = emailService;
        _emailOptions = emailOptions.Value;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancel = default)
    {
        if (string.IsNullOrEmpty(request.Email))
        {
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
                IsActive = u.IsActive,
                IsVerified = u.IsVerified
            })
            .FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancel);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
        {
            throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "Wrong email or password");
        }

        if (!user.IsActive)
        {
            throw new ForbiddenException(ErrorCodes.USER.INACTIVE, "User inactive");
        }

        var accessToken = _token.GenerateAccessToken(user.Id, user.Email);
        var refreshToken = _token.GenerateRefreshToken(user.Id, user.Email);

        var userRes = new UserResponse()
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            IsVerified = user.IsVerified,
            IsProfileCompleted = !string.IsNullOrEmpty(user.DisplayName) && !string.IsNullOrEmpty(user.Username),
            CreatedAt = DateTime.UtcNow
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

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var existingUserByEmail =
            await _dbContext.Users.AnyAsync(x => x.Email == normalizedEmail, cancel);
        if (existingUserByEmail)
        {
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
        return await _dbContext.SaveChangesAsync(cancel);
    }

    public async Task<bool> LogoutAsync(LogoutRequest logoutRequest, ClaimsPrincipal user, CancellationToken cancel = default)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || string.IsNullOrWhiteSpace(userIdClaim.Value))
        {
            throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "Invalid user");
        }

        var jtiClaim = user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
        var expClaim = user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Exp)?.Value;

        if (!string.IsNullOrEmpty(jtiClaim) && long.TryParse(expClaim, out var expSeconds))
        {
            var expirationTime = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
            var remainTtl = expirationTime - DateTime.UtcNow;

            if (remainTtl > TimeSpan.Zero)
            {
                await _redis.BlacklistAccessTokenAsync(jtiClaim, remainTtl);
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

        return true;
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

        if (await _redis.IsRefreshTokenBlacklistedAsync(request.RefreshToken))
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
