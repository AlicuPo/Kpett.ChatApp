using Kpett.ChatApp.Helpers;
using Kpett.ChatApp.Models;
using System.Security.Claims;

namespace Kpett.ChatApp.Services.Abstractions
{
    /// <summary>
    /// Service t?o v� x�c th?c JWT token (access + refresh).
    /// </summary>
    public interface IJwtService
    {
        /// <summary>L?y th�ng tin claims t? HttpContext hi?n t?i.</summary>
        UserClaims? GetUserClaims();

        /// <summary>T?o access token m?i.</summary>
        string GenerateAccessToken(string userId, string email, List<string>? roles = null);

        /// <summary>T?o refresh token m?i.</summary>
        string GenerateRefreshToken(string userId, string email);

        /// <summary>L?y ClaimsPrincipal t? token �? h?t h?n (d�ng cho refresh).</summary>
        ClaimsPrincipal? GetPrincipalFromExpiredToken(string token, bool isRefresh = false);
    }
}


