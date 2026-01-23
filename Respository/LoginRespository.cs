using Kpett.ChatApp.Entities;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Request;
using Kpett.ChatApp.Response;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Kpett.ChatApp.Services; 

namespace Kpett.ChatApp.Reposoitory
{
    public class LoginRespository
    {
        private readonly KpettChatAppContext _dbcontext;
        private readonly IToken _token; 

        public LoginRespository(KpettChatAppContext context, IToken token) 
        {
            _dbcontext = context;
            _token = token;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancel = default)
        {
            if (string.IsNullOrEmpty(request.UsernameOrEmail) || string.IsNullOrEmpty(request.Password))
            {
             
                throw new AppException(StatusCodes.Status400BadRequest, "Username not empty");
            }
            var user = await _dbcontext.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Username == request.UsernameOrEmail || u.Email == request.UsernameOrEmail, cancel);

            if (user == null)
            {
                throw new AppException(StatusCodes.Status404NotFound, "The account not found");
            }

            bool isPasswordValid = PasswordHasher.VerifyPasswordHash(request.Password, user.PasswordHash, user.PasswordSalt);

            if (!isPasswordValid)
            {
                throw new AppException(StatusCodes.Status401Unauthorized, "Incorrect password.");
            }
            if (user.IsActive == false) throw new AppException(StatusCodes.Status406NotAcceptable, "The account has been locked");


            var userRefreshTokens = await _dbcontext.Users.Include(u => u.RefreshTokens)
                    .FirstOrDefaultAsync(u => u.Username == request.UsernameOrEmail);
            var accessToken = _token.CreateToken(userRefreshTokens);

            var refreshToken = new RefreshToken
            {
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };
            userRefreshTokens.RefreshTokens.Add(refreshToken);

            var expiredTokens = userRefreshTokens.RefreshTokens.Where(t => t.ExpiresAt <= DateTime.UtcNow).ToList();
            foreach (var expiredToken in expiredTokens)
            {
                userRefreshTokens.RefreshTokens.Remove(expiredToken);
            }
            if (user.IsActive == false) throw new Exception("Tài khoản đã bị khóa.");

            //6.Tạo JWT Token
            //var token = _token.CreateToken(user);
            return new LoginResponse
            {
                //AccessToken = token,
                Username = user.Username,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl
            };

        }

        public async Task RegisterAsync(RegisterRequest request, CancellationToken cancel = default)
        {
            cancel.ThrowIfCancellationRequested();
            // Kiểm tra username và email đã tồn tại chưa   
            var existingUser = await _dbcontext.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username || u.Email == request.Email, cancel);

            if (existingUser != null)
            {
                if (existingUser.Username == request.Username)
                    throw new AppException(StatusCodes.Status400BadRequest, "Username really existing.");
                else
                    throw new AppException(StatusCodes.Status400BadRequest, "Email really existing.");
            }
            // Tạo password hash
            PasswordHasher.CreatePasswordHash(request.Password, out byte[] hash, out byte[] salt);
            // Tạo user mới
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = request.Username,
                Email = request.Email,
                PasswordHash = hash,
                PasswordSalt = salt,
                DisplayName = request.DisplayName,
                CreatedAt = DateTime.UtcNow, // Nên dùng DateTime.UtcNow
                IsActive = true,
                IsMuted = false
            };
            var userRole = await _dbcontext.Roles.FirstOrDefaultAsync(r => r.Name == "User", cancel);
            if (userRole != null)
            {
                user.UserRoles.Add(new UserRole { RoleId = userRole.Id });
            }

            _dbcontext.Users.Add(user);
            await _dbcontext.SaveChangesAsync(cancel);
        }
        public async Task<bool> LogoutAsync(Guid userId)
        {
            var user = await _dbcontext.Users
            .Include(u => u.RefreshTokens) 
            .FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
            {
                user.RefreshTokens.Clear();
                await _dbcontext.SaveChangesAsync();
                return true;
            }
            return false;
        }


    }
}
