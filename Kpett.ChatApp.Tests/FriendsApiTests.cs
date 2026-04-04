using System.Net;
using Kpett.ChatApp.DTOs.Response.Friend;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Tests.Infrastructure;

namespace Kpett.ChatApp.Tests;

public class FriendsApiTests
{
    [Fact]
    public async Task GetFriends_ReturnsFilteredResults_ByDisplayNameOrUsername()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("me", "me@example.com");

        await factory.SeedAsync(db =>
        {
            var currentUser = TestData.CreateUser("me", "me@example.com");
            var alice = TestData.CreateUser("alice-user", "alice@example.com");
            alice.Username = "alice.user";
            alice.DisplayName = "Alice Wonderland";

            var bob = TestData.CreateUser("bob-user", "bob@example.com");
            bob.Username = "builder.bob";
            bob.DisplayName = "Bob The Builder";

            var eve = TestData.CreateUser("eve-user", "eve@example.com");
            eve.Username = "eve.user";
            eve.DisplayName = "Eve Hacker";

            db.Users.AddRange(currentUser, alice, bob, eve);
            db.Friendships.AddRange(
                TestData.CreateAcceptedFriendship("me", "alice-user", DateTime.UtcNow.AddMinutes(-3)),
                TestData.CreateAcceptedFriendship("me", "bob-user", DateTime.UtcNow.AddMinutes(-2)));
        });

        var response = await client.GetAsync("/api/friends?search=Wonder&limit=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (_, body) = await HttpTestHelpers.ReadJsonAsync<GeneralResponse<PaginatedData<FriendListItemDTO>>>(response);
        Assert.True(body.IsSuccess);
        Assert.Single(body.Data.Items);
        Assert.Equal("alice-user", body.Data.Items[0].Id);
        Assert.Equal("alice.user", body.Data.Items[0].Username);
    }

    [Fact]
    public async Task GetFriends_SupportsCursorPagination()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("me", "me@example.com");

        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                TestData.CreateUser("me", "me@example.com"),
                TestData.CreateUser("friend-1", "friend1@example.com"),
                TestData.CreateUser("friend-2", "friend2@example.com"),
                TestData.CreateUser("friend-3", "friend3@example.com"));

            db.Friendships.AddRange(
                TestData.CreateAcceptedFriendship("me", "friend-1", new DateTime(2026, 4, 2, 8, 0, 0, DateTimeKind.Utc)),
                TestData.CreateAcceptedFriendship("me", "friend-2", new DateTime(2026, 4, 2, 9, 0, 0, DateTimeKind.Utc)),
                TestData.CreateAcceptedFriendship("me", "friend-3", new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc)));
        });

        var firstPageResponse = await client.GetAsync("/api/friends?limit=2");

        Assert.Equal(HttpStatusCode.OK, firstPageResponse.StatusCode);

        var (_, firstPage) = await HttpTestHelpers.ReadJsonAsync<GeneralResponse<PaginatedData<FriendListItemDTO>>>(firstPageResponse);
        Assert.True(firstPage.IsSuccess);
        Assert.Equal(2, firstPage.Data.Items.Count);
        Assert.Equal("friend-3", firstPage.Data.Items[0].Id);
        Assert.Equal("friend-2", firstPage.Data.Items[1].Id);
        Assert.NotNull(firstPage.Data.Pagination.NextCursor);

        var secondPageResponse = await client.GetAsync($"/api/friends?limit=2&cursor={Uri.EscapeDataString(firstPage.Data.Pagination.NextCursor!)}");

        Assert.Equal(HttpStatusCode.OK, secondPageResponse.StatusCode);

        var (_, secondPage) = await HttpTestHelpers.ReadJsonAsync<GeneralResponse<PaginatedData<FriendListItemDTO>>>(secondPageResponse);
        Assert.True(secondPage.IsSuccess);
        Assert.Single(secondPage.Data.Items);
        Assert.Equal("friend-1", secondPage.Data.Items[0].Id);
        Assert.Null(secondPage.Data.Pagination.NextCursor);
    }
}
