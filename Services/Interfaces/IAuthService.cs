using Kpett.ChatApp.DTOs.Response.Auth;
using Microsoft.AspNetCore.Identity.Data;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(DTOs.Request.Auth.LoginRequest request, CancellationToken cancel = default);
        Task<int> RegisterAsync(DTOs.Request.Auth.RegisterRequest request, CancellationToken cancel = default);
        Task<bool> LogoutAsync(string userId, CancellationToken cancel = default);
    }
}
