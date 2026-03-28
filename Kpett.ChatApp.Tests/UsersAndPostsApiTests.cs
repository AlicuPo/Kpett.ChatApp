using System.Net;
using System.Net.Http.Json;
using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Request.Post;
using Kpett.ChatApp.DTOs.Request.User;
using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.DTOs.Response.User;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace Kpett.ChatApp.Tests;

public class UsersAndPostsApiTests
{
    [Fact]
    public async Task GetUsers_ReturnsUnauthorized_WhenMissingToken()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync("/api/users/GetAllUser");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var error = await HttpTestHelpers.ReadErrorAsync(response);
        Assert.Equal(ErrorCodes.AUTH.UNAUTHORIZED, error.ErrorCode);
    }

    [Fact]
    public async Task InforUser_ReturnsWrappedCurrentUser()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("public-user", "public@example.com");

        await factory.SeedAsync(db =>
        {
            db.Users.Add(TestData.CreateUser("public-user", "public@example.com"));
        });

        var response = await client.PostAsJsonAsync("/api/users/inforUser", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (_, body) = await HttpTestHelpers.ReadJsonAsync<GeneralResponse<UserResponse>>(response);
        Assert.True(body.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, body.StatusCode);
        Assert.NotNull(body.Data);
        Assert.Equal("public-user", body.Data.Id);
    }

    [Fact]
    public async Task GetUserById_AllowsAnonymous()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateApiClient();

        await factory.SeedAsync(db =>
        {
            db.Users.Add(TestData.CreateUser("public-user", "public@example.com"));
        });

        var response = await client.GetAsync("/api/users/public-user");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (raw, body) = await HttpTestHelpers.ReadJsonAsync<UserResponse>(response);
        HttpTestHelpers.AssertRawSuccessPayload(raw);
        Assert.Equal("public-user", body.Id);
    }

    [Fact]
    public async Task UpdateAndDeleteCurrentUser_UseLegacyRoutesAndEnvelopes()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("user-1", "user-1@example.com");

        await factory.SeedAsync(db =>
        {
            db.Users.Add(TestData.CreateUser("user-1", "user-1@example.com"));
        });

        var updateResponse = await client.PutAsJsonAsync("/api/users/UpdateUser/user-1", new UpdateUserRequest
        {
            DisplayName = "Updated Name",
            Phone = "0123456789"
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var (_, updatedUser) = await HttpTestHelpers.ReadJsonAsync<GeneralResponse<UserResponse>>(updateResponse);
        Assert.True(updatedUser.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, updatedUser.StatusCode);
        Assert.NotNull(updatedUser.Data);
        Assert.Equal("Updated Name", updatedUser.Data.DisplayName);

        var deleteResponse = await client.DeleteAsync("/api/users/DeleteUser/user-1");

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var (_, deleteBody) = await HttpTestHelpers.ReadJsonAsync<GeneralResponse<bool>>(deleteResponse);
        Assert.True(deleteBody.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, deleteBody.StatusCode);
        Assert.True(deleteBody.Data);
    }

    [Fact]
    public async Task CreateUpdateAndDeletePost_UseExpectedStatusCodes()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("author-1", "author@example.com");

        await factory.SeedAsync(db =>
        {
            db.Users.Add(TestData.CreateUser("author-1", "author@example.com"));
        });

        var createResponse = await client.PostAsJsonAsync("/api/posts", new PostMediaRequest
        {
            Content = "My first post",
            Privacy = "Public"
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var (createRaw, createdPost) = await HttpTestHelpers.ReadJsonAsync<PostResponseDTO>(createResponse);
        HttpTestHelpers.AssertRawSuccessPayload(createRaw);
        Assert.Equal("My first post", createdPost.Content);
        Assert.NotNull(createResponse.Headers.Location);
        Assert.EndsWith($"/api/posts/{createdPost.Id}", createResponse.Headers.Location!.ToString(), StringComparison.Ordinal);

        var getResponse = await client.GetAsync($"/api/posts/{createdPost.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var updateResponse = await client.PatchAsJsonAsync($"/api/posts/{createdPost.Id}", new PostMediaRequest
        {
            Content = "Updated post",
            Privacy = "Friends"
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var (_, updatedPost) = await HttpTestHelpers.ReadJsonAsync<PostResponseDTO>(updateResponse);
        Assert.Equal("Updated post", updatedPost.Content);
        Assert.Equal("Friends", updatedPost.Privacy);

        var deleteResponse = await client.DeleteAsync($"/api/posts/{createdPost.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        await HttpTestHelpers.AssertNoContentAsync(deleteResponse);
    }

    [Fact]
    public async Task CommentsReactionsAndFeedEndpoints_UseExpectedStatusCodes()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("author-1", "author@example.com");

        await factory.SeedAsync(db =>
        {
            db.Users.Add(TestData.CreateUser("author-1", "author@example.com"));
            db.Users.Add(TestData.CreateUser("tagged-1", "tagged-1@example.com"));
            db.Users.Add(TestData.CreateUser("tagged-2", "tagged-2@example.com"));
            db.Posts.Add(TestData.CreatePost(100, "author-1", "Seeded post"));
            db.UserFeeds.Add(new UserFeed
            {
                Id = "feed-1",
                UserId = "author-1",
                PostId = 100,
                SourceUserId = "author-1",
                SourceType = "Post",
                CreatedAt = DateTime.UtcNow
            });
        });

        var reactionResponse = await client.PutAsJsonAsync("/api/posts/100/reactions/me", new UpsertReactionRequest
        {
            ReactionType = 1
        });

        Assert.Equal(HttpStatusCode.OK, reactionResponse.StatusCode);

        var (reactionRaw, reaction) = await HttpTestHelpers.ReadJsonAsync<PostReactionDTO>(reactionResponse);
        HttpTestHelpers.AssertRawSuccessPayload(reactionRaw);
        Assert.Equal((byte)1, reaction.Type);

        var getReactionsResponse = await client.GetAsync("/api/posts/100/reactions");

        Assert.Equal(HttpStatusCode.OK, getReactionsResponse.StatusCode);

        var (_, reactions) = await HttpTestHelpers.ReadJsonAsync<List<PostReactionDTO>>(getReactionsResponse);
        Assert.Single(reactions);

        var createCommentResponse = await client.PostAsJsonAsync("/api/posts/100/comments", new CreateCommentRequest
        {
            Content = "Nice post",
            Mentions = new List<string> { "tagged-1", "tagged-1" }
        });

        Assert.Equal(HttpStatusCode.Created, createCommentResponse.StatusCode);

        var (commentRaw, comment) = await HttpTestHelpers.ReadJsonAsync<CommentDTO>(createCommentResponse);
        HttpTestHelpers.AssertRawSuccessPayload(commentRaw);
        Assert.Equal("Nice post", comment.Content);
        Assert.Equal(0, comment.LikeCount);
        Assert.Equal(0, comment.ReplyCount);
        Assert.False(comment.IsEdited);
        Assert.NotNull(comment.Mentions);
        var createdMentions = Assert.Single(comment.Mentions!);
        Assert.Equal("tagged-1", createdMentions.UserId);
        Assert.Equal("tagged-1", createdMentions.Username);
        Assert.NotNull(createCommentResponse.Headers.Location);
        Assert.EndsWith($"/api/comments/{comment.Id}", createCommentResponse.Headers.Location!.ToString(), StringComparison.Ordinal);

        var createReplyResponse = await client.PostAsJsonAsync("/api/posts/100/comments", new CreateCommentRequest
        {
            Content = "Reply comment",
            ParentCommentId = comment.Id,
            Mentions = new List<string> { "tagged-2" }
        });

        Assert.Equal(HttpStatusCode.Created, createReplyResponse.StatusCode);

        var (_, reply) = await HttpTestHelpers.ReadJsonAsync<CommentDTO>(createReplyResponse);
        Assert.Equal(comment.Id, reply.ParentCommentId);
        Assert.NotNull(reply.Mentions);
        Assert.Equal("tagged-2", Assert.Single(reply.Mentions!).UserId);

        var getCommentsResponse = await client.GetAsync("/api/posts/100/comments");

        Assert.Equal(HttpStatusCode.OK, getCommentsResponse.StatusCode);

        var (_, commentsResponse) = await HttpTestHelpers.ReadJsonAsync<GeneralResponse<CommentsPageDTO>>(getCommentsResponse);
        Assert.True(commentsResponse.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, commentsResponse.StatusCode);
        Assert.NotNull(commentsResponse.Data);
        var rootComment = Assert.Single(commentsResponse.Data.Items);
        Assert.Equal(comment.Id, rootComment.Id);
        Assert.Null(rootComment.ParentId);
        Assert.NotNull(rootComment.Author);
        Assert.Equal("author-1", rootComment.Author.Id);
        Assert.Equal("author-1", rootComment.Author.Username);
        Assert.Equal("author-1", rootComment.Author.DisplayName);
        Assert.NotNull(rootComment.Mentions);
        Assert.Equal("tagged-1", Assert.Single(rootComment.Mentions).UserId);
        Assert.NotNull(rootComment.Attachments);
        Assert.Empty(rootComment.Attachments);
        Assert.NotNull(rootComment.Metrics);
        Assert.Equal(0, rootComment.Metrics.LikeCount);
        Assert.Equal(1, rootComment.Metrics.ReplyCount);
        Assert.NotNull(rootComment.ViewerContext);
        Assert.False(rootComment.ViewerContext.IsLiked);
        Assert.True(rootComment.ViewerContext.CanEdit);
        Assert.True(rootComment.ViewerContext.CanDelete);
        Assert.True(rootComment.ViewerContext.CanReply);
        Assert.False(rootComment.IsEdited);
        Assert.False(rootComment.IsDeleted);
        Assert.NotNull(commentsResponse.Data.Pagination);
        Assert.False(commentsResponse.Data.Pagination.HasMore);
        Assert.Null(commentsResponse.Data.Pagination.NextCursor);
        Assert.Equal(20, commentsResponse.Data.Pagination.Limit);
        Assert.Equal(1, commentsResponse.Data.Pagination.TotalCount);

        var updateCommentResponse = await client.PatchAsJsonAsync($"/api/comments/{comment.Id}", new UpdateCommentRequest
        {
            Content = "Edited comment",
            Mentions = new List<string> { "tagged-2" }
        });

        Assert.Equal(HttpStatusCode.OK, updateCommentResponse.StatusCode);

        var (_, updatedComment) = await HttpTestHelpers.ReadJsonAsync<CommentDTO>(updateCommentResponse);
        Assert.Equal("Edited comment", updatedComment.Content);
        Assert.True(updatedComment.IsEdited);
        Assert.NotNull(updatedComment.Mentions);
        Assert.Equal("tagged-2", Assert.Single(updatedComment.Mentions!).UserId);

        var feedResponse = await client.GetAsync("/api/users/me/feed");
        Assert.Equal(HttpStatusCode.OK, feedResponse.StatusCode);

        var (_, feeds) = await HttpTestHelpers.ReadJsonAsync<List<UserFeedDTO>>(feedResponse);
        Assert.Single(feeds);

        var userPostsResponse = await client.GetAsync("/api/users/author-1/posts");
        Assert.Equal(HttpStatusCode.OK, userPostsResponse.StatusCode);

        var (_, userPosts) = await HttpTestHelpers.ReadJsonAsync<List<PostResponseDTO>>(userPostsResponse);
        Assert.Single(userPosts);

        var deleteReplyResponse = await client.DeleteAsync($"/api/comments/{reply.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteReplyResponse.StatusCode);
        await HttpTestHelpers.AssertNoContentAsync(deleteReplyResponse);

        var getCommentsAfterDeleteResponse = await client.GetAsync("/api/posts/100/comments");

        Assert.Equal(HttpStatusCode.OK, getCommentsAfterDeleteResponse.StatusCode);

        var (_, commentsAfterDeleteResponse) = await HttpTestHelpers.ReadJsonAsync<GeneralResponse<CommentsPageDTO>>(getCommentsAfterDeleteResponse);
        var rootCommentAfterDelete = Assert.Single(commentsAfterDeleteResponse.Data.Items);
        Assert.Equal(0, rootCommentAfterDelete.Metrics.ReplyCount);

        var deleteCommentResponse = await client.DeleteAsync($"/api/comments/{comment.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteCommentResponse.StatusCode);
        await HttpTestHelpers.AssertNoContentAsync(deleteCommentResponse);

        var removeReactionResponse = await client.DeleteAsync("/api/posts/100/reactions/me");

        Assert.Equal(HttpStatusCode.NoContent, removeReactionResponse.StatusCode);
        await HttpTestHelpers.AssertNoContentAsync(removeReactionResponse);
    }

    [Fact]
    public async Task CreateComment_ReturnsBadRequest_WhenMentionUserDoesNotExist()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("author-1", "author@example.com");

        await factory.SeedAsync(db =>
        {
            db.Users.Add(TestData.CreateUser("author-1", "author@example.com"));
            db.Posts.Add(TestData.CreatePost(200, "author-1", "Seeded post"));
        });

        var response = await client.PostAsJsonAsync("/api/posts/200/comments", new CreateCommentRequest
        {
            Content = "Tagging a missing user",
            Mentions = new List<string> { "missing-user" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await HttpTestHelpers.ReadErrorAsync(response);
        Assert.Equal(ErrorCodes.VALIDATION.REQUIRED, error.ErrorCode);
    }

    [Fact]
    public async Task GetMissingPost_ReturnsNotFound()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("user-1", "user-1@example.com");

        await factory.SeedAsync(db =>
        {
            db.Users.Add(TestData.CreateUser("user-1", "user-1@example.com"));
        });

        var response = await client.GetAsync("/api/posts/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await HttpTestHelpers.ReadErrorAsync(response);
        Assert.Equal(ErrorCodes.POST.NOT_FOUND, error.ErrorCode);
    }
}
