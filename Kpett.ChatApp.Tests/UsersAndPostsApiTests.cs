using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Request.Post;
using Kpett.ChatApp.DTOs.Request.User;
using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.DTOs.Response.User;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

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

        var createResponse = await client.PostAsJsonAsync("/api/posts", new PostRequest
        {
            Content = "My first post",
            Privacy = "Public"
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var (_, createBody) = await HttpTestHelpers.ReadJsonAsync<GeneralResponse<string>>(createResponse);
        Assert.True(createBody.IsSuccess);
        Assert.Equal(201, createBody.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(createBody.Data));

        var createdPostId = createBody.Data;

        var getResponse = await client.GetAsync($"/api/posts/{createdPostId}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var (_, createdPost) = await HttpTestHelpers.ReadJsonAsync<PostResponseDTO>(getResponse);
        Assert.Equal("My first post", createdPost.Content);
        Assert.Equal(createdPostId, createdPost.Id);

        var updateResponse = await client.PatchAsJsonAsync($"/api/posts/{createdPostId}", new PostRequest
        {
            Content = "Updated post",
            Privacy = "Friends"
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var (_, updatedPost) = await HttpTestHelpers.ReadJsonAsync<PostResponseDTO>(updateResponse);
        Assert.Equal("Updated post", updatedPost.Content);
        Assert.Equal("Friends", updatedPost.Privacy);

        var deleteResponse = await client.DeleteAsync($"/api/posts/{createdPostId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        await HttpTestHelpers.AssertNoContentAsync(deleteResponse);
    }

    [Fact]
    public async Task CommentsReactionsAndFeedEndpoints_UseExpectedStatusCodes()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("author-1", "author@example.com");
        const string postId = "11111111-1111-1111-1111-111111111111";

        await factory.SeedAsync(db =>
        {
            db.Users.Add(TestData.CreateUser("author-1", "author@example.com"));
            db.Users.Add(TestData.CreateUser("tagged-1", "tagged-1@example.com"));
            db.Users.Add(TestData.CreateUser("tagged-2", "tagged-2@example.com"));
            db.Posts.Add(TestData.CreatePost(postId, "author-1", "Seeded post"));
            db.UserFeeds.Add(new UserFeed
            {
                Id = "feed-1",
                UserId = "author-1",
                PostId = postId,
                SourceUserId = "author-1",
                SourceType = "Post",
                CreatedAt = DateTime.UtcNow
            });
        });

        var reactionResponse = await client.PutAsJsonAsync($"/api/posts/{postId}/reactions/me", new UpsertReactionRequest
        {
            ReactionType = 1
        });

        Assert.Equal(HttpStatusCode.OK, reactionResponse.StatusCode);

        var (reactionRaw, reaction) = await HttpTestHelpers.ReadJsonAsync<PostReactionDTO>(reactionResponse);
        HttpTestHelpers.AssertRawSuccessPayload(reactionRaw);
        Assert.False(string.IsNullOrWhiteSpace(reaction.Id));
        Assert.Equal((byte)1, reaction.Type);

        var getReactionsResponse = await client.GetAsync($"/api/posts/{postId}/reactions");

        Assert.Equal(HttpStatusCode.OK, getReactionsResponse.StatusCode);

        var (_, reactions) = await HttpTestHelpers.ReadJsonAsync<List<PostReactionDTO>>(getReactionsResponse);
        Assert.Single(reactions);

        var createCommentResponse = await client.PostAsJsonAsync($"/api/posts/{postId}/comments", new CreateCommentRequest
        {
            Content = "<@tagged-1> Nice post <@tagged-1>"
        });

        Assert.Equal(HttpStatusCode.Created, createCommentResponse.StatusCode);

        var (commentRaw, comment) = await HttpTestHelpers.ReadJsonAsync<CommentDTO>(createCommentResponse);
        HttpTestHelpers.AssertRawSuccessPayload(commentRaw);
        Assert.Equal("<@tagged-1> Nice post <@tagged-1>", comment.Content);
        Assert.Equal(0, comment.LikeCount);
        Assert.Equal(0, comment.ReplyCount);
        Assert.False(comment.IsEdited);
        Assert.NotNull(comment.Mentions);
        var createdMentions = Assert.Single(comment.Mentions!);
        Assert.Equal("tagged-1", createdMentions.UserId);
        Assert.Equal("tagged-1", createdMentions.Username);
        Assert.NotNull(createCommentResponse.Headers.Location);
        Assert.EndsWith($"/api/comments/{comment.Id}", createCommentResponse.Headers.Location!.ToString(), StringComparison.Ordinal);

        var createReplyResponse = await client.PostAsJsonAsync($"/api/posts/{postId}/comments", new CreateCommentRequest
        {
            Content = "<@tagged-2> Reply comment",
            ParentCommentId = $" {comment.Id} "
        });

        Assert.Equal(HttpStatusCode.Created, createReplyResponse.StatusCode);

        var (_, reply) = await HttpTestHelpers.ReadJsonAsync<CommentDTO>(createReplyResponse);
        Assert.Equal(comment.Id, reply.ParentCommentId);
        Assert.NotNull(reply.Mentions);
        Assert.Equal("tagged-2", Assert.Single(reply.Mentions!).UserId);

        var getCommentsResponse = await client.GetAsync($"/api/posts/{postId}/comments");

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
            Content = "<@tagged-2> Edited comment"
        });

        Assert.Equal(HttpStatusCode.OK, updateCommentResponse.StatusCode);

        var (_, updatedComment) = await HttpTestHelpers.ReadJsonAsync<CommentDTO>(updateCommentResponse);
        Assert.Equal("<@tagged-2> Edited comment", updatedComment.Content);
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

        Assert.Equal(HttpStatusCode.OK, deleteReplyResponse.StatusCode);
        var (_, deleteReplyBody) = await HttpTestHelpers.ReadJsonAsync<GeneralResponse>(deleteReplyResponse);
        Assert.True(deleteReplyBody.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, deleteReplyBody.StatusCode);

        var getCommentsAfterDeleteResponse = await client.GetAsync($"/api/posts/{postId}/comments");

        Assert.Equal(HttpStatusCode.OK, getCommentsAfterDeleteResponse.StatusCode);

        var (_, commentsAfterDeleteResponse) = await HttpTestHelpers.ReadJsonAsync<GeneralResponse<CommentsPageDTO>>(getCommentsAfterDeleteResponse);
        var rootCommentAfterDelete = Assert.Single(commentsAfterDeleteResponse.Data.Items);
        Assert.Equal(0, rootCommentAfterDelete.Metrics.ReplyCount);

        var deleteCommentResponse = await client.DeleteAsync($"/api/comments/{comment.Id}");

        Assert.Equal(HttpStatusCode.OK, deleteCommentResponse.StatusCode);
        var (_, deleteCommentBody) = await HttpTestHelpers.ReadJsonAsync<GeneralResponse>(deleteCommentResponse);
        Assert.True(deleteCommentBody.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, deleteCommentBody.StatusCode);

        var removeReactionResponse = await client.DeleteAsync($"/api/posts/{postId}/reactions/me");

        Assert.Equal(HttpStatusCode.NoContent, removeReactionResponse.StatusCode);
        await HttpTestHelpers.AssertNoContentAsync(removeReactionResponse);
    }

    [Fact]
    public async Task CreateComment_ReturnsBadRequest_WhenMentionUserDoesNotExist()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("author-1", "author@example.com");
        const string postId = "22222222-2222-2222-2222-222222222222";

        await factory.SeedAsync(db =>
        {
            db.Users.Add(TestData.CreateUser("author-1", "author@example.com"));
            db.Posts.Add(TestData.CreatePost(postId, "author-1", "Seeded post"));
        });

        var response = await client.PostAsJsonAsync($"/api/posts/{postId}/comments", new CreateCommentRequest
        {
            Content = "Tagging a missing user <@missing-user>"
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

        var response = await client.GetAsync("/api/posts/99999999-9999-9999-9999-999999999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await HttpTestHelpers.ReadErrorAsync(response);
        Assert.Equal(ErrorCodes.POST.NOT_FOUND, error.ErrorCode);
    }

    [Fact]
    public async Task GenerateCommentsMockData_CreatesCommentsRepliesMentionsAndLikes()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateApiClient();

        await factory.SeedAsync(db =>
        {
            db.Users.Add(TestData.CreateUser("user-1", "user-1@example.com"));
            db.Users.Add(TestData.CreateUser("user-2", "user-2@example.com"));
            db.Users.Add(TestData.CreateUser("user-3", "user-3@example.com"));
            db.Posts.Add(TestData.CreatePost("post-1", "user-1", "Seeded post 1"));
            db.Posts.Add(TestData.CreatePost("post-2", "user-2", "Seeded post 2"));
        });

        var response = await client.PostAsync(
            "/api/MockData/generate-comments?numberOfPosts=1&minCommentsPerPost=2&maxCommentsPerPost=2&minRepliesPerComment=1&maxRepliesPerComment=1&minMentionsPerComment=1&maxMentionsPerComment=1&minLikesPerComment=1&maxLikesPerComment=1",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var summaryJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var summary = summaryJson.RootElement;
        Assert.Equal(1, summary.GetProperty("postsProcessed").GetInt32());
        Assert.Equal(2, summary.GetProperty("rootComments").GetInt32());
        Assert.Equal(2, summary.GetProperty("replies").GetInt32());
        Assert.Equal(4, summary.GetProperty("totalComments").GetInt32());
        Assert.Equal(4, summary.GetProperty("mentions").GetInt32());
        Assert.Equal(4, summary.GetProperty("commentLikes").GetInt32());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var comments = dbContext.Comments.ToList();
        var rootComments = comments.Where(c => string.IsNullOrEmpty(c.ParentCommentId)).ToList();
        var replies = comments.Where(c => !string.IsNullOrEmpty(c.ParentCommentId)).ToList();

        Assert.Equal(4, comments.Count);
        Assert.Equal(2, rootComments.Count);
        Assert.Equal(2, replies.Count);
        Assert.All(rootComments, comment => Assert.Equal(1, comment.ReplyCount));
        Assert.All(comments, comment => Assert.Equal(1, comment.LikeCount));
        Assert.Equal(4, dbContext.MentionComments.Count());
        Assert.Equal(4, dbContext.CommentLikes.Count());
    }
}
