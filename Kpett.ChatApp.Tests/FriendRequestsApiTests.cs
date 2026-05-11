using System.Net;
using System.Net.Http.Json;
using Kpett.ChatApp.Constants;
using Kpett.ChatApp.DTOs.Request.Friend;
using Kpett.ChatApp.DTOs.Response.Friend;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kpett.ChatApp.Tests;

public class FriendRequestsApiTests
{
    [Fact]
    public async Task CreateFriendRequest_ReturnsCreated_WithLocation()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("sender-1", "sender@example.com");

        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                TestData.CreateUser("sender-1", "sender@example.com"),
                TestData.CreateUser("receiver-1", "receiver@example.com"));
        });

        var response = await client.PostAsJsonAsync("/api/friend-requests", new CreateFriendRequestRequest
        {
            ReceiverId = "receiver-1"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var (raw, body) = await HttpTestHelpers.ReadJsonAsync<FriendRequestDTO>(response);
        HttpTestHelpers.AssertRawSuccessPayload(raw);
        Assert.Equal("sender-1", body.SenderId);
        Assert.Equal(FriendshipsEnums.Pending.GetDescription(), body.Status);
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"/api/friend-requests/{body.FriendRequestId}", response.Headers.Location!.ToString(), StringComparison.Ordinal);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedRequest = await dbContext.FriendRequests.SingleAsync(fr => fr.Id == body.FriendRequestId);

        Assert.Equal("sender-1", savedRequest.SenderId);
        Assert.Equal("receiver-1", savedRequest.ReceiverId);
        Assert.Equal("sender-1", savedRequest.UserHighId);
        Assert.Equal("receiver-1", savedRequest.UserLowId);
    }

    [Fact]
    public async Task GetPendingFriendRequests_ReturnsOk()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("receiver-1", "receiver@example.com");

        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                TestData.CreateUser("sender-1", "sender@example.com"),
                TestData.CreateUser("receiver-1", "receiver@example.com"));
            db.FriendRequests.Add(TestData.CreatePendingFriendRequest("friend-request-1", "sender-1", "receiver-1"));
        });

        var response = await client.GetAsync("/api/friend-requests?status=pending");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (raw, body) = await HttpTestHelpers.ReadJsonAsync<List<FriendRequestDTO>>(response);
        HttpTestHelpers.AssertRawSuccessPayload(raw);
        Assert.Single(body);
        Assert.Equal("friend-request-1", body[0].FriendRequestId);
    }

    [Fact]
    public async Task UpdateFriendRequestStatus_ReturnsOk_WhenAccepted()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("receiver-1", "receiver@example.com");

        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                TestData.CreateUser("sender-1", "sender@example.com"),
                TestData.CreateUser("receiver-1", "receiver@example.com"));
            db.FriendRequests.Add(TestData.CreatePendingFriendRequest("friend-request-1", "sender-1", "receiver-1"));
        });

        var response = await client.PatchAsJsonAsync("/api/friend-requests/friend-request-1", new UpdateFriendRequestStatusRequest
        {
            Status = "accepted"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (raw, body) = await HttpTestHelpers.ReadJsonAsync<FriendRequestDTO>(response);
        HttpTestHelpers.AssertRawSuccessPayload(raw);
        Assert.Equal(FriendshipsEnums.Accepted.GetDescription(), body.Status);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var friendship = await dbContext.Friendships.SingleAsync();

        Assert.Equal("receiver-1", friendship.ActionUserId);
        Assert.Equal(FriendshipsEnums.Accepted.GetDescription(), friendship.Status);
    }

    [Fact]
    public async Task UpdateFriendRequestStatus_ReturnsOk_WhenAcceptIsRetriedAfterFriendshipAlreadyExists()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("receiver-1", "receiver@example.com");

        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                TestData.CreateUser("sender-1", "sender@example.com"),
                TestData.CreateUser("receiver-1", "receiver@example.com"));
            db.FriendRequests.Add(TestData.CreatePendingFriendRequest("friend-request-1", "sender-1", "receiver-1"));
            db.Friendships.Add(TestData.CreateAcceptedFriendship("sender-1", "receiver-1"));
        });

        var response = await client.PatchAsJsonAsync("/api/friend-requests/friend-request-1", new UpdateFriendRequestStatusRequest
        {
            Status = "accepted"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (_, body) = await HttpTestHelpers.ReadJsonAsync<FriendRequestDTO>(response);
        Assert.Equal(FriendshipsEnums.Accepted.GetDescription(), body.Status);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedRequest = await dbContext.FriendRequests.SingleAsync(fr => fr.Id == "friend-request-1");
        Assert.Equal(FriendshipsEnums.Accepted.GetDescription(), savedRequest.Status);
    }

    [Fact]
    public async Task UpdateFriendRequestStatus_ReturnsForbidden_WhenSenderTriesToAccept()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("sender-1", "sender@example.com");

        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                TestData.CreateUser("sender-1", "sender@example.com"),
                TestData.CreateUser("receiver-1", "receiver@example.com"));
            db.FriendRequests.Add(TestData.CreatePendingFriendRequest("friend-request-1", "sender-1", "receiver-1"));
        });

        var response = await client.PatchAsJsonAsync("/api/friend-requests/friend-request-1", new UpdateFriendRequestStatusRequest
        {
            Status = "accepted"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var error = await HttpTestHelpers.ReadErrorAsync(response);
        Assert.Equal(ErrorCodes.AUTH.FORBIDDEN, error.ErrorCode);
    }

    [Fact]
    public async Task CancelFriendRequest_ReturnsNoContent()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("sender-1", "sender@example.com");

        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                TestData.CreateUser("sender-1", "sender@example.com"),
                TestData.CreateUser("receiver-1", "receiver@example.com"));
            db.FriendRequests.Add(TestData.CreatePendingFriendRequest("friend-request-1", "sender-1", "receiver-1"));
        });

        var response = await client.DeleteAsync("/api/friend-requests/friend-request-1");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await HttpTestHelpers.AssertNoContentAsync(response);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedRequest = await dbContext.FriendRequests.SingleAsync(fr => fr.Id == "friend-request-1");
        Assert.Equal(FriendshipsEnums.Cancelled.GetDescription(), savedRequest.Status);
    }

    [Fact]
    public async Task CancelFriendRequest_ReturnsForbidden_WhenReceiverTriesToCancel()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("receiver-1", "receiver@example.com");

        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                TestData.CreateUser("sender-1", "sender@example.com"),
                TestData.CreateUser("receiver-1", "receiver@example.com"));
            db.FriendRequests.Add(TestData.CreatePendingFriendRequest("friend-request-1", "sender-1", "receiver-1"));
        });

        var response = await client.DeleteAsync("/api/friend-requests/friend-request-1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var error = await HttpTestHelpers.ReadErrorAsync(response);
        Assert.Equal(ErrorCodes.AUTH.FORBIDDEN, error.ErrorCode);
    }

    [Fact]
    public async Task UpdateFriendRequestStatus_ReturnsBadRequest_WhenStatusUnsupported()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("receiver-1", "receiver@example.com");

        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                TestData.CreateUser("sender-1", "sender@example.com"),
                TestData.CreateUser("receiver-1", "receiver@example.com"));
            db.FriendRequests.Add(TestData.CreatePendingFriendRequest("friend-request-1", "sender-1", "receiver-1"));
        });

        var response = await client.PatchAsJsonAsync("/api/friend-requests/friend-request-1", new UpdateFriendRequestStatusRequest
        {
            Status = "cancelled"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await HttpTestHelpers.ReadErrorAsync(response);
        Assert.Equal(ErrorCodes.VALIDATION.REQUIRED, error.ErrorCode);
    }

    [Fact]
    public async Task CreateFriendRequest_ReturnsConflict_WhenSameDirectionPendingRequestAlreadyExists()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("sender-1", "sender@example.com");

        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                TestData.CreateUser("sender-1", "sender@example.com"),
                TestData.CreateUser("receiver-1", "receiver@example.com"));
            db.FriendRequests.Add(TestData.CreatePendingFriendRequest("friend-request-1", "sender-1", "receiver-1"));
        });

        var response = await client.PostAsJsonAsync("/api/friend-requests", new CreateFriendRequestRequest
        {
            ReceiverId = "receiver-1"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var error = await HttpTestHelpers.ReadErrorAsync(response);
        Assert.Equal(ErrorCodes.FRIEND.FRIEND_REQUEST_PENDING, error.ErrorCode);
    }

    [Fact]
    public async Task CreateFriendRequest_ReturnsOkAndAccepts_WhenReversePendingRequestAlreadyExists()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("receiver-1", "receiver@example.com");

        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                TestData.CreateUser("sender-1", "sender@example.com"),
                TestData.CreateUser("receiver-1", "receiver@example.com"));
            db.FriendRequests.Add(TestData.CreatePendingFriendRequest("friend-request-1", "sender-1", "receiver-1"));
        });

        var response = await client.PostAsJsonAsync("/api/friend-requests", new CreateFriendRequestRequest
        {
            ReceiverId = "sender-1"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (raw, body) = await HttpTestHelpers.ReadJsonAsync<FriendRequestDTO>(response);
        HttpTestHelpers.AssertRawSuccessPayload(raw);
        Assert.Equal("friend-request-1", body.FriendRequestId);
        Assert.Equal(FriendshipsEnums.Accepted.GetDescription(), body.Status);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedRequest = await dbContext.FriendRequests.SingleAsync(fr => fr.Id == "friend-request-1");
        var friendship = await dbContext.Friendships.SingleAsync();

        Assert.Equal(FriendshipsEnums.Accepted.GetDescription(), savedRequest.Status);
        Assert.Equal(FriendshipsEnums.Accepted.GetDescription(), friendship.Status);
        Assert.Equal("receiver-1", friendship.ActionUserId);
    }

    [Theory]
    [InlineData("Rejected")]
    [InlineData("Cancelled")]
    public async Task CreateFriendRequest_ReusesExistingRow_WhenPreviousRequestWasClosed(string previousStatus)
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("receiver-1", "receiver@example.com");

        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                TestData.CreateUser("sender-1", "sender@example.com"),
                TestData.CreateUser("receiver-1", "receiver@example.com"));
            db.FriendRequests.Add(TestData.CreateFriendRequest(
                "friend-request-1",
                "sender-1",
                "receiver-1",
                previousStatus,
                createdAt: DateTime.UtcNow.AddDays(-2),
                updatedAt: DateTime.UtcNow.AddDays(-1)));
        });

        var response = await client.PostAsJsonAsync("/api/friend-requests", new CreateFriendRequestRequest
        {
            ReceiverId = "sender-1"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (raw, body) = await HttpTestHelpers.ReadJsonAsync<FriendRequestDTO>(response);
        HttpTestHelpers.AssertRawSuccessPayload(raw);
        Assert.Equal("friend-request-1", body.FriendRequestId);
        Assert.Equal("receiver-1", body.SenderId);
        Assert.Equal(FriendshipsEnums.Pending.GetDescription(), body.Status);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedRequest = await dbContext.FriendRequests.SingleAsync(fr => fr.Id == "friend-request-1");

        Assert.Equal("friend-request-1", savedRequest.Id);
        Assert.Equal("receiver-1", savedRequest.SenderId);
        Assert.Equal("sender-1", savedRequest.ReceiverId);
        Assert.Equal(FriendshipsEnums.Pending.GetDescription(), savedRequest.Status);
        Assert.Equal(1, await dbContext.FriendRequests.CountAsync());
    }
}

