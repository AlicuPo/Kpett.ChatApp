using System.Security.Claims;
using Kpett.ChatApp.Constants;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Helper;
using Microsoft.AspNetCore.Http;

namespace Kpett.ChatApp.Tests.Helpers;

public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetRequiredUserId_ReturnsNameIdentifierClaim()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-123")],
            authenticationType: "Test"));

        var userId = user.GetRequiredUserId();

        Assert.Equal("user-123", userId);
    }

    [Fact]
    public void GetRequiredUserId_ThrowsUnauthorized_WhenClaimIsMissing()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Test"));

        var exception = Assert.Throws<UnauthorizedException>(() => user.GetRequiredUserId());

        Assert.Equal(ErrorCodes.AUTH.UNAUTHORIZED, exception.ErrorCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, exception.StatusCode);
    }
}
