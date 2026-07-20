using Kpett.ChatApp.Helpers;
using Kpett.ChatApp.Models;
using System.Security.Claims;

namespace Kpett.ChatApp.Services.Interfaces
{
    /// <summary>
    /// Service t?o và xác th?c JWT token (access + refresh).
    /// </summary>
    public interface IJwtService
    {
        /// <summary>L?y thông tin claims t? HttpContext hi?n t?i.</summary>
        UserClaims? GetUserClaims();

        /// <summary>T?o access token m?i.</summary>
        string GenerateAccessToken(string userId, string email);

        /// <summary>T?o refresh token m?i.</summary>
        string GenerateRefreshToken(string userId, string email);

        /// <summary>L?y ClaimsPrincipal t? token ð? h?t h?n (dùng cho refresh).</summary>
        ClaimsPrincipal? GetPrincipalFromExpiredToken(string token, bool isRefresh = false);
    }
}
