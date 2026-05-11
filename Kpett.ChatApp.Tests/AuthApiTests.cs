using System.Net;
using System.Net.Http.Json;
using Kpett.ChatApp.Constants;
using Kpett.ChatApp.DTOs.Request.Auth;
using Kpett.ChatApp.DTOs.Response.Auth;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Services.Interfaces;
using Kpett.ChatApp.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kpett.ChatApp.Tests;

public class AuthApiTests
{
    [Fact]
    public async Task Register_ReturnsOk_WithLegacyEnvelope()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = "new-user@example.com",
            Password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (_, body) = await HttpTestHelpers.ReadJsonAsync<GeneralResponse>(response);
        Assert.True(body.IsSuccess);
        Assert.Equal(StatusCodes.Status201Created, body.StatusCode);
        Assert.Equal("Register successfully.", body.Message);
    }

    [Fact]
    public async Task Register_ReturnsConflict_WhenEmailAlreadyExists()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateApiClient();

        await factory.SeedAsync(db =>
        {
            db.Users.Add(TestData.CreateUser("existing-user", "existing@example.com"));
        });

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = "existing@example.com",
            Password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var error = await HttpTestHelpers.ReadErrorAsync(response);
        Assert.Equal(ErrorCodes.USER.ALREADY_EXISTS_BY_EMAIL, error.ErrorCode);
    }

    [Fact]
    public async Task Login_ReturnsOk_WithLegacyEnvelope()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateApiClient();

        await factory.SeedAsync(db =>
        {
            db.Users.Add(TestData.CreateUser("login-user", "login@example.com"));
        });

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "login@example.com",
            Password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (_, body) = await HttpTestHelpers.ReadJsonAsync<GeneralResponse<LoginResponse>>(response);
        Assert.True(body.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, body.StatusCode);
        Assert.Equal("Login successfully.", body.Message);
        Assert.NotNull(body.Data);
        var login = body.Data;
        Assert.Equal("login-user", login.User.Id);
        Assert.False(string.IsNullOrWhiteSpace(login.Token.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(login.Token.RefreshToken));
    }

    [Fact]
    public async Task Refresh_ReturnsOk_WhenRefreshTokenIsValid()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateApiClient();
        using var scope = factory.Services.CreateScope();

        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
        var redisService = scope.ServiceProvider.GetRequiredService<IRedisService>();
        var refreshToken = jwtService.GenerateRefreshToken("refresh-user", "refresh@example.com");

        await redisService.SaveRefreshTokenAsync("refresh-user", refreshToken, TimeSpan.FromDays(30));

        var response = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = refreshToken
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (_, body) = await HttpTestHelpers.ReadJsonAsync<GeneralResponse<TokenResponse>>(response);
        Assert.True(body.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, body.StatusCode);
        Assert.Equal("Token refreshed successfully.", body.Message);
        Assert.NotNull(body.Data);
        Assert.False(string.IsNullOrWhiteSpace(body.Data.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.Data.RefreshToken));
    }

    [Fact]
    public async Task Logout_ReturnsOk_WithLegacyEnvelope()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("logout-user", "logout@example.com");

        await factory.SeedAsync(db =>
        {
            db.Users.Add(TestData.CreateUser("logout-user", "logout@example.com"));
        });

        var response = await client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest
        {
            RefreshToken = "refresh-token"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (_, body) = await HttpTestHelpers.ReadJsonAsync<GeneralResponse>(response);
        Assert.True(body.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, body.StatusCode);
        Assert.Equal("Logout successful.", body.Message);
    }

    [Fact]
    public async Task RevokeCurrentToken_ReturnsOk_WithLegacyEnvelope()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("revoke-user", "revoke@example.com");

        var response = await client.PostAsync("/api/auth/revoke", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (_, body) = await HttpTestHelpers.ReadJsonAsync<GeneralResponse>(response);
        Assert.True(body.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, body.StatusCode);
        Assert.Equal("Token revoked successfully.", body.Message);
    }
}

