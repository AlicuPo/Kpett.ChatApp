using System.Security.Claims;
using Kpett.ChatApp.Contants;
using Kpett.ChatApp.Exceptions;

namespace Kpett.ChatApp.Helper
{
    public static class ClaimsPrincipalExtensions
    {
        public static string? GetRequiredUserId(this ClaimsPrincipal? user)
        {
            var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

            return userId;
        }
    }
}
