using System.Security.Claims;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Helper;

namespace Kpett.ChatApp.Tests;

public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetRequiredUserId_ReturnsNameIdentifierClaim()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "user-123")
                },
                authenticationType: "TestAuth"));

        var userId = principal.GetRequiredUserId();

        Assert.Equal("user-123", userId);
    }

    [Fact]
    public void GetRequiredUserId_ThrowsWhenClaimMissing()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.Throws<UnauthorizedException>(() => principal.GetRequiredUserId());
    }
}
