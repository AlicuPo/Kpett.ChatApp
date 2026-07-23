using Kpett.ChatApp.DTOs.Request.Post;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;

namespace Kpett.ChatApp.Services.Abstractions
{
    /// <summary>
    /// Service qu?n l? bài vi?t: CRUD bài vi?t, feed, nhóm bài vi?t, reaction (u? quy?n cho <see cref="IPostReactionService"/>).
    /// </summary>
    public interface IPostService
    {
        /// <summary>T?o bài vi?t m?i.</summary>
        Task<PostFeedResponse> CreatePostAsync(string userId, PostRequest postRequest, CancellationToken cancel);

        /// <summary>T?o bài vi?t trong nhóm.</summary>
        Task<PostFeedResponse> CreateGroupPostAsync(string userId, string groupId, PostRequest postRequest, CancellationToken cancel);

        /// <summary>C?p nh?t tr?ng thái bài vi?t nhóm (duy?t/t? ch?i).</summary>
        Task<PostFeedResponse> UpdateGroupPostStatusAsync(string userId, string groupId, string postId, UpdateGroupPostStatusRequest request, CancellationToken cancel);

        /// <summary>Ghim/b? ghim bài vi?t trong nhóm.</summary>
        Task<PostFeedResponse> TogglePinPostAsync(string userId, string groupId, string postId, CancellationToken cancel);

        /// <summary>C?p nh?t n?i dung bài vi?t.</summary>
        Task<PostFeedResponse> UpdatePostAsync(string postId, string userId, PostRequest postRequest, CancellationToken cancel);

        /// <summary>L?y bài vi?t theo ID.</summary>
        Task<PostFeedResponse> GetPostByIdAsync(string postId, string? currentUserId, CancellationToken cancel);

        /// <summary>L?y feed bài vi?t v?i cursor-based pagination.</summary>
        Task<PaginatedData<PostFeedResponse>> GetFeedAsync(string? currentUserId, string? cursor = null, int limit = 10, CancellationToken cancel = default);

        /// <summary>L?y bài vi?t trong nhóm.</summary>
        Task<PaginatedData<PostFeedResponse>> GetGroupPostsAsync(string? currentUserId, string groupId, CursorPaginationRequest request, string? status = null, CancellationToken cancel = default);

        /// <summary>L?y bài vi?t c?a ngý?i dùng c? th?.</summary>
        Task<PaginatedData<PostThumbnailResponse>> GetPostsByUserIdAsync(string userId, string? currentUserId, SearchRequest request, CursorPaginationRequest cursorPagination, CancellationToken cancel = default);

        /// <summary>Xoá bài vi?t (soft delete).</summary>
        Task DeletePostAsync(string postId, string userId, CancellationToken cancel);

        // ??? Reaction operations (delegated to IPostReactionService) ???

        /// <summary>Thêm reaction vào bài vi?t.</summary>
        Task<PostReactionDTO> AddReactionAsync(string postId, string userId, byte reactionType, CancellationToken cancel);

        /// <summary>Xoá reaction kh?i bài vi?t.</summary>
        Task RemoveReactionAsync(string postId, string userId, CancellationToken cancel);

        /// <summary>L?y danh sách reaction c?a bài vi?t.</summary>
        Task<List<PostReactionDTO>> GetPostReactionsAsync(string postId, CancellationToken cancel);
    }
}


