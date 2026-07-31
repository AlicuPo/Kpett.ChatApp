using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;

namespace Kpett.ChatApp.Services.Abstractions
{
    /// <summary>
    /// Service qu?n l? b?nh lu?n: th�m, s?a, xo�, like/unlike, l?y danh s�ch.
    /// </summary>
    public interface ICommentService
    {
        /// <summary>Th�m b?nh lu?n v�o b�i vi?t (h? tr? reply).</summary>
        Task<CommentListItemDTO> AddCommentAsync(string postId, string userId, string content, string? parentCommentId, CancellationToken cancel);

        /// <summary>L?y danh s�ch b?nh lu?n (cursor pagination).</summary>
        Task<PaginatedData<CommentListItemDTO>> GetCommentsAsync(string postId, string parentCommentId, string? currentUserId, string? cursor, int limit, CancellationToken cancel);

        /// <summary>C?p nh?t n?i dung b?nh lu?n.</summary>
        Task<CommentListItemDTO> UpdateCommentAsync(string commentId, string userId, string content, CancellationToken cancel);

        /// <summary>Xo� b?nh lu?n.</summary>
        Task DeleteCommentAsync(string commentId, string userId, CancellationToken cancel);

        /// <summary>Like b?nh lu?n.</summary>
        Task<CommentListItemDTO> LikeCommentAsync(string commentId, string userId, CancellationToken cancel);

        /// <summary>B? like b?nh lu?n.</summary>
        Task<CommentListItemDTO> UnlikeCommentAsync(string commentId, string userId, CancellationToken cancel);
    }
}


