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
                throw new Exception("Username/Email và mật khẩu không được để trống.");
            }
            var user = await _dbcontext.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Username == request.UsernameOrEmail || u.Email == request.UsernameOrEmail, cancel);

            if (user == null)
            {
                throw new Exception("Tài khoản không tồn tại.");
            }

            bool isPasswordValid = PasswordHasher.VerifyPasswordHash(request.Password, user.PasswordHash, user.PasswordSalt);

            if (!isPasswordValid)
            {
                throw new Exception("Mật khẩu không chính xác.");
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
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request), "Request cannot be null");
            }
            PasswordHasher.CreatePasswordHash(request.Password, out byte[] hash, out byte[] salt);
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = request.Username,
                Email = request.Email,
                PasswordHash = hash,
                PasswordSalt = salt,
                DisplayName = request.DisplayName,
                CreatedAt = DateTime.Now,
                IsActive = true,
                IsMuted = false,              
            };
            _dbcontext.Users.Add(user);
            await _dbcontext.SaveChangesAsync(cancel);
        }


    }
}
