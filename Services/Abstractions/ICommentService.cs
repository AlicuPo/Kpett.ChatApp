using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;

namespace Kpett.ChatApp.Services.Abstractions
{
    /// <summary>
    /// Service qu?n l? b?nh lu?n: thêm, s?a, xoá, like/unlike, l?y danh sách.
    /// </summary>
    public interface ICommentService
    {
        /// <summary>Thêm b?nh lu?n vào bài vi?t (h? tr? reply).</summary>
        Task<CommentListItemDTO> AddCommentAsync(string postId, string userId, string content, string? parentCommentId, CancellationToken cancel);

        /// <summary>L?y danh sách b?nh lu?n (cursor pagination).</summary>
        Task<PaginatedData<CommentListItemDTO>> GetCommentsAsync(string postId, string parentCommentId, string currentUserId, string? cursor, int limit, CancellationToken cancel);

        /// <summary>C?p nh?t n?i dung b?nh lu?n.</summary>
        Task<CommentListItemDTO> UpdateCommentAsync(string commentId, string userId, string content, CancellationToken cancel);

        /// <summary>Xoá b?nh lu?n.</summary>
        Task DeleteCommentAsync(string commentId, string userId, CancellationToken cancel);

        /// <summary>Like b?nh lu?n.</summary>
        Task<CommentListItemDTO> LikeCommentAsync(string commentId, string userId, CancellationToken cancel);

        /// <summary>B? like b?nh lu?n.</summary>
        Task<CommentListItemDTO> UnlikeCommentAsync(string commentId, string userId, CancellationToken cancel);
    }
}


