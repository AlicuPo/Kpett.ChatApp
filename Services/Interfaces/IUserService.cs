using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IUserService
    {
        //Task<(List<UserResponse>, int)> GetAllUser(UserRequest search, CancellationToken cancel = default);
        Task<UserResponse> inforUser(UserRequest Request, CancellationToken cancel);
    }
}
