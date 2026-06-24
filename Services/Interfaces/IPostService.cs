using Kpett.ChatApp.DTOs.Request.Post;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IPostService
    {
        // Post operations
        Task<PostFeedResponse> CreatePostAsync(string userId, PostRequest postRequest, CancellationToken cancel);
        Task<PostFeedResponse> CreateGroupPostAsync(string userId, string groupId, PostRequest postRequest, CancellationToken cancel);
        Task<PostFeedResponse> UpdateGroupPostStatusAsync(string userId, string groupId, string postId, UpdateGroupPostStatusRequest request, CancellationToken cancel);
        Task<PostFeedResponse> UpdatePostAsync(string postId, string userId, PostRequest postRequest, CancellationToken cancel);
        Task<PostFeedResponse> GetPostByIdAsync(string postId, string? currentUserId, CancellationToken cancel);
        Task<PaginatedData<PostFeedResponse>> GetFeedAsync(string? currentUserId, string? cursor = null, int limit = 10, CancellationToken cancel = default);
        Task<PaginatedData<PostFeedResponse>> GetGroupPostsAsync(string? currentUserId, string groupId, CursorPaginationRequest request, CancellationToken cancel = default);
        Task<PaginatedData<PostThumbnailResponse>> GetPostsByUserIdAsync(string userId, string? currentUserId, SearchRequest request, CursorPaginationRequest cursorPagination, CancellationToken cancel = default);
        Task DeletePostAsync(string postId, string userId, CancellationToken cancel);
        // Reaction operations
        Task<PostReactionDTO> AddReactionAsync(string postId, string userId, byte reactionType, CancellationToken cancel);
        Task RemoveReactionAsync(string postId, string userId, CancellationToken cancel);
        Task<List<PostReactionDTO>> GetPostReactionsAsync(string postId, CancellationToken cancel);
    }
}
