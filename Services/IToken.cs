using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using System.Security.Claims;

namespace Kpett.ChatApp.Services
{
    public interface IToken
    {
        UserClaims? GetUserClaims();
        string GenerateAccessToken(string userId, string UserName);
        string GenerateRefreshToken(string userId, string UserName);
        ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
    }
}
