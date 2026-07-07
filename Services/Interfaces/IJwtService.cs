using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using System.Security.Claims;

namespace Kpett.ChatApp.Services.Interfaces
{
    /// <summary>
    /// Service tạo và xác thực JWT token (access + refresh).
    /// </summary>
    public interface IJwtService
    {
        /// <summary>Lấy thông tin claims từ HttpContext hiện tại.</summary>
        UserClaims? GetUserClaims();

        /// <summary>Tạo access token mới.</summary>
        string GenerateAccessToken(string userId, string email);

        /// <summary>Tạo refresh token mới.</summary>
        string GenerateRefreshToken(string userId, string email);

        /// <summary>Lấy ClaimsPrincipal từ token đã hết hạn (dùng cho refresh).</summary>
        ClaimsPrincipal? GetPrincipalFromExpiredToken(string token, bool isRefresh = false);
    }
}
