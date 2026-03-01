using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Helper;
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

            var user = await _dbcontext.Users.FindAsync(Request.Id, cancel);
            if (user == null)
            {
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");
            }

            return new UserResponse
            {
                Id = user.Id,
                Name = user.Name,
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
                query = query.Where(u => u.Name.Contains(search.Search) || u.DisplayName.Contains(search.Search) || u.Email.Contains(search.Search));
            }

            if (search.Status.HasValue)
            {
                // Implement status filter if needed, currently User model has string Status but request has int Status. 
                // Assuming logic for status mapping or ignoring if not applicable directly.
            }

            var totalCount = await query.CountAsync(cancel);

            var users = await query
                .Skip((search.Page - 1) * search.PageSize)
                .Take(search.PageSize)
                .Select(u => new UserResponse
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    DisplayName = u.DisplayName,
                    AvatarUrl = u.AvatarUrl,
                    CreatedAt = u.CreatedAt
                    // Add other fields map if needed
                })
                .ToListAsync(cancel);

            return (users, totalCount);
        }

        public async Task<UserResponse> UpdateUser(string id, UpdateUserRequest request, CancellationToken cancel)
        {
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
                Name = user.Name,
                Email = user.Email,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl,
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<bool> DeleteUser(string id, CancellationToken cancel)
        {
            var user = await _dbcontext.Users.FindAsync(new object[] { id }, cancel);
            if (user == null)
            {
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");
            }

            user.IsActive = false;
            // Optionally update Status string to "Deleted" or similar if required

            await _dbcontext.SaveChangesAsync(cancel);
            return true;
        }
    }
}
