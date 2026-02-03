using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services;

namespace Kpett.ChatApp.Respository
{
    public class UserRespository : IUsers
    {
        private readonly AppDbContext _dbcontext;
        
        public UserRespository(AppDbContext dbContext)
        {
            _dbcontext = dbContext;
        }

        public async Task<UserResponse> inforUser(UserRequest Request, CancellationToken cancel)
        {
           if(Request.Id == null)
            {
                throw new AppException(StatusCodes.Status400BadRequest,"Id cannot be null");
            }
     
            var user = await _dbcontext.Users.FindAsync(Request.Id, cancel);
            if (user == null)
            {
                throw new AppException(StatusCodes.Status400BadRequest,"User not found");
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

        
    }
}
