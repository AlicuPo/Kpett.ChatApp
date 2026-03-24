using System.Net;
using System.Net.Http.Json;
using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Request.Friend;
using Kpett.ChatApp.DTOs.Response.Friend;
using Kpett.ChatApp.Tests.Infrastructure;

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
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"/api/friend-requests/{body.FriendRequestId}", response.Headers.Location!.ToString(), StringComparison.Ordinal);
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
        Assert.Equal("Accepted", body.Status);
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
    public async Task CreateFriendRequest_ReturnsConflict_WhenPendingRequestAlreadyExists()
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
}
