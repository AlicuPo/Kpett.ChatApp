using Kpett.ChatApp.DTOs.Request.User;
using Kpett.ChatApp.DTOs.Response.User;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IUserService
    {
        Task<UsernameCheckResponse> CheckExistByUsername(string username, CancellationToken cancel);
        Task<(List<UserResponse>, int)> GetAllUser(UserRequest search, CancellationToken cancel = default);
        Task<UserResponse> inforUser(UserRequest Request, CancellationToken cancel);
        Task<UserResponse> UpdateUser(string id, string currentUserId, UpdateUserRequest request, CancellationToken cancel);
        Task<bool> DeleteUser(string id, string currentUserId, CancellationToken cancel);
        Task<UserResponse> AccountSetup(string userId, AccountSetupRequest accountSetupRequest, CancellationToken cancel);
    }
}
