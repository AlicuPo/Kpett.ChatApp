using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Kpett.ChatApp.Configs;
using Kpett.ChatApp.Services.Impls;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kpett.ChatApp.Tests.Services;

public class JwtServiceTests
{
    [Fact]
    public void GenerateAccessToken_CanBeValidatedWithConfiguredAccessKey()
    {
        var service = CreateService();

        var token = service.GenerateAccessToken("user-123", "user@example.com");
        var principal = service.GetPrincipalFromExpiredToken(token);

        Assert.Equal("user-123", principal?.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("user@example.com", principal?.FindFirstValue(ClaimTypes.Email));
        Assert.NotNull(principal?.FindFirstValue(JwtRegisteredClaimNames.Jti));
    }

    [Fact]
    public void GenerateRefreshToken_CanBeValidatedWithConfiguredRefreshKey()
    {
        var service = CreateService();

        var token = service.GenerateRefreshToken("user-123", "user@example.com");
        var principal = service.GetPrincipalFromExpiredToken(token, isRefresh: true);

        Assert.Equal("user-123", principal?.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("user@example.com", principal?.FindFirstValue(ClaimTypes.Email));
    }

    [Fact]
    public void GetUserClaims_ReturnsClaimsFromCurrentHttpContext()
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "user-123"),
                        new Claim(ClaimTypes.Name, "kpett"),
                        new Claim(ClaimTypes.Email, "user@example.com"),
                        new Claim(ClaimTypes.Role, "Admin"),
                        new Claim("DisplayName", "Kpett User"),
                        new Claim("AvatarUrl", "https://example.com/avatar.png"),
                        new Claim(JwtRegisteredClaimNames.Jti, "token-id")
                    ],
                    authenticationType: "Test"))
            }
        };
        var service = CreateService(accessor);

        var claims = service.GetUserClaims();

        Assert.NotNull(claims);
        Assert.Equal("user-123", claims.UserId);
        Assert.Equal("kpett", claims.Username);
        Assert.Equal("Kpett User", claims.DisplayName);
        Assert.Equal("https://example.com/avatar.png", claims.AvatarUrl);
        Assert.Equal("user@example.com", claims.Email);
        Assert.Equal("token-id", claims.Jti);
        Assert.True(claims.IsInRole("admin"));
    }

    [Fact]
    public void GetUserClaims_ReturnsNull_WhenRequiredClaimsAreMissing()
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "user-123")],
                    authenticationType: "Test"))
            }
        };
        var service = CreateService(accessor);

        var claims = service.GetUserClaims();

        Assert.Null(claims);
    }

    private static JwtService CreateService(IHttpContextAccessor? accessor = null)
    {
        var options = new JwtOptions
        {
            KeyAccess = "access-token-test-key-with-at-least-32-chars",
            KeyRefres = "refresh-token-test-key-with-at-least-32-chars",
            Issuer = "Kpett.ChatApp.Tests",
            Audience = "Kpett.ChatApp.Tests",
            AccessTokenExpireMinutes = 15,
            RefreshTokenExpireDays = 7
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSection:KeyAccess"] = options.KeyAccess,
                ["JwtSection:KeyRefres"] = options.KeyRefres
            })
            .Build();

        return new JwtService(
            accessor ?? new HttpContextAccessor(),
            global::Microsoft.Extensions.Options.Options.Create(options),
            configuration,
            NullLogger<JwtService>.Instance);
    }
}
