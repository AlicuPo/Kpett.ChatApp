using Kpett.ChatApp.Response;
using Microsoft.AspNetCore.Identity.Data;

namespace Kpett.ChatApp.Services
{
    public interface ILogin
    {
        Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancel = default);
        Task RegisterAsync(RegisterRequest request, CancellationToken cancel = default);
    }
}
