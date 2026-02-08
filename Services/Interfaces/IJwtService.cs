using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using System.Security.Claims;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IJwtService
    {
        UserClaims? GetUserClaims();
        string GenerateAccessToken(string userId, string UserName, string? email = null, string? displayName = null);
        string GenerateRefreshToken(string userId, string UserName, string? email = null);
        ClaimsPrincipal? GetPrincipalFromExpiredToken(string token, bool isRefresh = false);
    }
}
