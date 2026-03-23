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
        if (string.IsNullOrEmpty(request.Email))
        {
            throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Email not empty");
        }
        
        var user = await _dbcontext.Users.FirstOrDefaultAsync(x => x.Email == request.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
        {
            throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "Wrong email or password");
        }

        if (!user.IsActive)
        {
            throw new ForbiddenException(ErrorCodes.USER.INACTIVE, "User inactive");
        }

        // Tạo JWT Token
        var accessToken = _token.GenerateAccessToken(user.Id, user.Name, user.Email, user.DisplayName);
        var refreshToken = _token.GenerateRefreshToken(user.Id, user.Name, user.Email);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl
        };
    }

    private void ValidateRegisterRequest(RegisterRequest request)
    {
        if (string.IsNullOrEmpty(request.Password) || string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Email))
        {
            throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Username or Password or Email is required");
        }
    }   

    public async Task<int> RegisterAsync(RegisterRequest request, CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        
        ValidateRegisterRequest(request);

        var existingUserByEmail =
            await _dbcontext.Users.AnyAsync(x => x.Email == request.Email);
        if (existingUserByEmail)
        {
            throw new ConflictException(ErrorCodes.USER.ALREADY_EXISTS_BY_EMAIL, "Email really existing");
        }

        var existingUserByUsername =
            await _dbcontext.Users.AnyAsync(x => x.Name == request.Username);
        if (existingUserByUsername)
        {
            throw new ConflictException(ErrorCodes.USER.ALREADY_EXISTS_BY_USERNAME, "Username really existing");
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
        var user = await _dbcontext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancel);
        if (user != null)
        {
            user.Status = UserStatusEnums.Offline.GetDescription();
            await _dbcontext.SaveChangesAsync(cancel);
        }

        return true;
    }
}