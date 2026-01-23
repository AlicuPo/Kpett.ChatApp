using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;

namespace Kpett.ChatApp.Services
{
    public interface IUsers
    {
        Task<(List<UserResponse>, int)> GetAllUser(UserRequest search, CancellationToken cancel = default);
    }
}
