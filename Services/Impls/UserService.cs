using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Request.User;
using Kpett.ChatApp.DTOs.Response.User;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Kpett.ChatApp.Services.Impls
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _dbcontext;

        public UserService(AppDbContext dbContext)
        {
            _dbcontext = dbContext;
        }

        public async Task<UserResponse> inforUser(UserRequest Request, CancellationToken cancel)
        {
            if (Request.Id == null)
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Id cannot be null");
            }

            var user = await _dbcontext.Users.FindAsync(new object[] { Request.Id }, cancel);
            if (user == null)
            {
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");
            }

            return new UserResponse
            {
                Id = user.Id,
                Username = user.Name,
                Email = user.Email,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl,
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<(List<UserResponse>, int)> GetAllUser(UserRequest search, CancellationToken cancel = default)
        {
            var query = _dbcontext.Users.AsQueryable().AsNoTracking();

            if (!string.IsNullOrEmpty(search.Search))
            {
                query = query.Where(u =>
                    u.Name.Contains(search.Search) ||
                    (u.DisplayName != null && u.DisplayName.Contains(search.Search)) ||
                    (u.Email != null && u.Email.Contains(search.Search)));
            }

            var totalCount = await query.CountAsync(cancel);

            var users = await query
                .Skip((search.Page - 1) * search.PageSize)
                .Take(search.PageSize)
                .Select(u => new UserResponse
                {
                    Id = u.Id,
                    Username = u.Name,
                    Email = u.Email,
                    DisplayName = u.DisplayName,
                    AvatarUrl = u.AvatarUrl,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync(cancel);

            return (users, totalCount);
        }

        public async Task<UserResponse> UpdateUser(string id, string currentUserId, UpdateUserRequest request, CancellationToken cancel)
        {
            if (id != currentUserId)
            {
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You can only update your own profile.");
            }

            var user = await _dbcontext.Users.FindAsync(new object[] { id }, cancel);
            if (user == null)
            {
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");
            }

            if (!string.IsNullOrEmpty(request.DisplayName)) user.DisplayName = request.DisplayName;
            if (!string.IsNullOrEmpty(request.AvatarUrl)) user.AvatarUrl = request.AvatarUrl;
            if (!string.IsNullOrEmpty(request.Phone)) user.Phone = request.Phone;
            if (!string.IsNullOrEmpty(request.Gender)) user.Gender = request.Gender;

            user.UpdatedAt = DateTime.UtcNow;

            await _dbcontext.SaveChangesAsync(cancel);

            return new UserResponse
            {
                Id = user.Id,
                Username = user.Name,
                Email = user.Email,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl,
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<bool> DeleteUser(string id, string currentUserId, CancellationToken cancel)
        {
            if (id != currentUserId)
            {
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You can only delete your own profile.");
            }

            var user = await _dbcontext.Users.FindAsync(new object[] { id }, cancel);
            if (user == null)
            {
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");
            }

            user.IsActive = false;

            await _dbcontext.SaveChangesAsync(cancel);
            return true;
        }
    }
}
