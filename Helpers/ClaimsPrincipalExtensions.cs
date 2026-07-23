using System.Security.Claims;
using Kpett.ChatApp.Constants;
using Kpett.ChatApp.Exceptions;

namespace Kpett.ChatApp.Helpers
{
    public static class ClaimsPrincipalExtensions
    {
        public static string GetRequiredUserId(this ClaimsPrincipal? user)
        {
            var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

            userId ??= user?.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.NameId);
            userId ??= user?.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

            if (userId == null)
            {
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");
            }

            return userId;
        }
    }
}

