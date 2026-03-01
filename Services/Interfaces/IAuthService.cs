using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;
using Microsoft.AspNetCore.Identity.Data;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(DTOs.Request.LoginRequest request, CancellationToken cancel = default);
        Task<int> RegisterAsync(DTOs.Request.RegisterRequest request, CancellationToken cancel = default);
        Task<bool> LogoutAsync(string userId, CancellationToken cancel = default);
    }
}
