using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;
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

    private readonly AppDbContext _dbcontext;
    private readonly IRedisService _redis;
    private readonly IJwtService _token;

    public AuthService(AppDbContext context, IJwtService token, IRedisService redis, ICloudinaryService cloudinary)
    {
        _dbcontext = context;
        _token = token;
        _redis = redis;
        _cloudinary = cloudinary;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancel = default)
    {
        if (string.IsNullOrEmpty(request.UsernameOrEmail))
        {
            throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Username not empty");
        }

        var user = await _dbcontext.Users
            .FirstOrDefaultAsync(x =>
                x.Email == request.UsernameOrEmail || x.Name == request.UsernameOrEmail);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
        {
            throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "The account not found");
        }

        if (!user.IsActive)
        {
            throw new ForbiddenException(ErrorCodes.USER.INACTIVE, "User inactive");
        }

        var status = UserStatusEnums.Online.GetDescription();
        user.Status = status;
        await _dbcontext.SaveChangesAsync(cancel);

        await _redis.RemoveRefreshTokenAsync(user.Id);

        // Tạo JWT Token
        var accessToken = _token.GenerateAccessToken(user.Id, user.Name, user.Email, user.DisplayName);
        var refreshToken = _token.GenerateRefreshToken(user.Id, user.Name, user.Email);
        await _redis.SaveRefreshTokenAsync(user.Id, refreshToken, TimeSpan.FromDays(30));

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = 30 * 60,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl
        };
    }


    public async Task<int> RegisterAsync(RegisterRequest request, CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();

        var existingUser =
            await _dbcontext.Users.FirstOrDefaultAsync(x => x.Email == request.Email || x.Name == request.Username);

        if (existingUser != null)
        {
            throw new ConflictException(ErrorCodes.USER.ALREADY_EXISTS, "Username or Email really existing");
        }

        if (string.IsNullOrEmpty(request.Password) || string.IsNullOrEmpty(request.Username))
        {
            throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Username and Password is required");
        }
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var avatarUrl = string.Empty;

        var _id = Uuid.NewDatabaseFriendly(Database.SqlServer).ToString("N");

        var newUser = new User
        {
            Id = _id,
            Name = request.Username ?? string.Empty,
            Password = hashedPassword,
            Email = request.Email,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Status = UserStatusEnums.Offline.GetDescription()
        };
        _dbcontext.Users.Add(newUser);
        var result = await _dbcontext.SaveChangesAsync(cancel);
        return result;
    }

    public async Task<bool> LogoutAsync(string userId, CancellationToken cancel = default)
    {
        // Xoá refresh token trong Redis
        await _redis.RemoveRefreshTokenAsync(userId);

        // Cập nhật trạng thái user trong DB
        var user = await _dbcontext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancel);
        if (user != null)
        {
            user.Status = UserStatusEnums.Offline.GetDescription();
            await _dbcontext.SaveChangesAsync(cancel);
        }

        return true;
    }
}