using Kpett.ChatApp.DTOs.Request.Auth;
using Kpett.ChatApp.DTOs.Response.Auth;
using System.Security.Claims;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancel = default);
        Task<int> RegisterAsync(RegisterRequest request, CancellationToken cancel = default);
        Task<bool> LogoutAsync(LogoutRequest logoutRequest , ClaimsPrincipal user, CancellationToken cancel = default);
        Task<TokenResponse> RefreshTokenAsync(RefreshTokenRequest request);
    }
}
