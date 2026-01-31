using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using UUIDNext;

namespace Kpett.ChatApp.Reposoitory
{
    public class LoginRespository : ILogin
    {

        private readonly AppDbContext _dbcontext;
        private readonly IToken _token;
        private readonly Services.IRedis _redis;
        private readonly ICloudinary _cloudinary;

        public LoginRespository(AppDbContext context, IToken token, Services.IRedis redis, ICloudinary cloudinary)
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

                throw new AppException(StatusCodes.Status400BadRequest, "Username not empty");
            }
            var user = await _dbcontext.Users
              .FirstOrDefaultAsync(x =>
                  x.Email == request.UsernameOrEmail || x.Name == request.UsernameOrEmail);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            {
                throw new AppException(StatusCodes.Status404NotFound, "The account not found");
            }
            if (!user.IsActive)
                throw new AppException(StatusCodes.Status401Unauthorized, "User inactive");

            var status = EnumHelper.GetDescription(UserStatusEnums.Online);
            user.Status = status;
            await _dbcontext.SaveChangesAsync(cancel);

            await _redis.RemoveRefreshTokenAsync(user.Id);

            // Tạo JWT Token
            var accessToken = _token.GenerateAccessToken(user.Id,user.Name);
            var refreshToken = _token.GenerateRefreshToken(user.Id, user.Name);
            await _redis.SaveRefreshTokenAsync(user.Id, refreshToken, TimeSpan.FromDays(30));

            return new LoginResponse
            {
                
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = 30 * 60,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl
            };
        }


        public async Task<int> RegisterAsync(RegisterRequest request, CancellationToken cancel = default)
        {
            cancel.ThrowIfCancellationRequested();

            var existingUser = await _dbcontext.Users.FirstOrDefaultAsync(x => x.Email == request.Email || x.Name == request.Username);

            if (existingUser != null) throw new AppException(StatusCodes.Status400BadRequest, "Username or Email really existing");
            if (string.IsNullOrEmpty(request.Password) || string.IsNullOrEmpty(request.Username))
            {
                throw new AppException(StatusCodes.Status400BadRequest, "Email and Password or Username is null");
            }
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

            string avatarUrl = string.Empty;           

            string _id = Uuid.NewDatabaseFriendly(Database.SqlServer).ToString("N");

            var newUser = new User
            {
                Id = _id,
                Name = request.Username ?? string.Empty,
                Password = hashedPassword,
                Email = request.Email,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Status = EnumHelper.GetDescription(UserStatusEnums.Offline)
            };
            _dbcontext.Users.Add(newUser);
            var result = await _dbcontext.SaveChangesAsync(cancel);
            return result;
        }

        public async Task<bool> LogoutAsync(string userId, CancellationToken cancel = default)
        {
            // Xoá refresh token trong Redis
            await _redis.RemoveRefreshTokenAsync(userId.ToString());

            // Cập nhật trạng thái user trong DB
            var user = await _dbcontext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancel);
            if (user != null)
            {
                user.Status = EnumHelper.GetDescription(UserStatusEnums.Offline);
                await _dbcontext.SaveChangesAsync(cancel);
            }

            return true;
        }
    }
}
