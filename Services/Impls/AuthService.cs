using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Request.Auth;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.DTOs.Response.Auth;
using Kpett.ChatApp.DTOs.Response.User;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
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
                Username = u.Name,
                Email = u.Email,
                DisplayName = u.DisplayName,
                AvatarUrl = u.AvatarUrl,
                Password = u.Password,
                IsActive = u.IsActive
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

        // Tạo JWT Token
        var accessToken = _token.GenerateAccessToken(user.Id, user.Username, user.Email, user.DisplayName);
        var refreshToken = _token.GenerateRefreshToken(user.Id, user.Username, user.Email);

        var userRes = new UserResponse()
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            isProfileCompleted = !string.IsNullOrEmpty(user.DisplayName) && !string.IsNullOrEmpty(user.Username),
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

    public async Task<bool> LogoutAsync(string userId, CancellationToken cancel = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancel);
        if (user != null)
        {
            user.Status = UserStatusEnums.Offline.GetDescription();
            await _dbContext.SaveChangesAsync(cancel);
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

}