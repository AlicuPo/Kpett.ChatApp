using Kpett.ChatApp.DTOs.Request.Post;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;

namespace Kpett.ChatApp.Services.Interfaces
{
    /// <summary>
    /// Service quản lý bài viết: CRUD bài viết, feed, nhóm bài viết, reaction (uỷ quyền cho <see cref="IPostReactionService"/>).
    /// </summary>
    public interface IPostService
    {
        /// <summary>Tạo bài viết mới.</summary>
        Task<PostFeedResponse> CreatePostAsync(string userId, PostRequest postRequest, CancellationToken cancel);

        /// <summary>Tạo bài viết trong nhóm.</summary>
        Task<PostFeedResponse> CreateGroupPostAsync(string userId, string groupId, PostRequest postRequest, CancellationToken cancel);

        /// <summary>Cập nhật trạng thái bài viết nhóm (duyệt/từ chối).</summary>
        Task<PostFeedResponse> UpdateGroupPostStatusAsync(string userId, string groupId, string postId, UpdateGroupPostStatusRequest request, CancellationToken cancel);

        /// <summary>Cập nhật nội dung bài viết.</summary>
        Task<PostFeedResponse> UpdatePostAsync(string postId, string userId, PostRequest postRequest, CancellationToken cancel);

        /// <summary>Lấy bài viết theo ID.</summary>
        Task<PostFeedResponse> GetPostByIdAsync(string postId, string? currentUserId, CancellationToken cancel);

        /// <summary>Lấy feed bài viết với cursor-based pagination.</summary>
        Task<PaginatedData<PostFeedResponse>> GetFeedAsync(string? currentUserId, string? cursor = null, int limit = 10, CancellationToken cancel = default);

        /// <summary>Lấy bài viết trong nhóm.</summary>
        Task<PaginatedData<PostFeedResponse>> GetGroupPostsAsync(string? currentUserId, string groupId, CursorPaginationRequest request, string? status = null, CancellationToken cancel = default);

        /// <summary>Lấy bài viết của người dùng cụ thể.</summary>
        Task<PaginatedData<PostThumbnailResponse>> GetPostsByUserIdAsync(string userId, string? currentUserId, SearchRequest request, CursorPaginationRequest cursorPagination, CancellationToken cancel = default);

        /// <summary>Xoá bài viết (soft delete).</summary>
        Task DeletePostAsync(string postId, string userId, CancellationToken cancel);

        // ─── Reaction operations (delegated to IPostReactionService) ───

        /// <summary>Thêm reaction vào bài viết.</summary>
        Task<PostReactionDTO> AddReactionAsync(string postId, string userId, byte reactionType, CancellationToken cancel);

        /// <summary>Xoá reaction khỏi bài viết.</summary>
        Task RemoveReactionAsync(string postId, string userId, CancellationToken cancel);

        /// <summary>Lấy danh sách reaction của bài viết.</summary>
        Task<List<PostReactionDTO>> GetPostReactionsAsync(string postId, CancellationToken cancel);
    }
}
