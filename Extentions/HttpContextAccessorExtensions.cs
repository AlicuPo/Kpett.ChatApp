using System.Security.Claims;

namespace Kpett.ChatApp.Extentions
{
    public static class HttpContextAccessorExtensions
    {
        /// <summary>
        /// Lấy UserId dưới dạng string từ JWT Token thông qua HttpContext
        /// </summary>
        public static string? GetUserId(this IHttpContextAccessor httpContextAccessor)
        {
            var user = httpContextAccessor.HttpContext?.User;

            if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
            {
                return null;
            }

            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);

            return userIdClaim?.Value;
        }
    }
}
