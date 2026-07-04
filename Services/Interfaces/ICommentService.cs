using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;

namespace Kpett.ChatApp.Services.Interfaces
{
    /// <summary>
    /// Service quản lý bình luận: thêm, sửa, xoá, like/unlike, lấy danh sách.
    /// </summary>
    public interface ICommentService
    {
        /// <summary>Thêm bình luận vào bài viết (hỗ trợ reply).</summary>
        Task<CommentListItemDTO> AddCommentAsync(string postId, string userId, string content, string? parentCommentId, CancellationToken cancel);

        /// <summary>Lấy danh sách bình luận (cursor pagination).</summary>
        Task<PaginatedData<CommentListItemDTO>> GetCommentsAsync(string postId, string parentCommentId, string currentUserId, string? cursor, int limit, CancellationToken cancel);

        /// <summary>Cập nhật nội dung bình luận.</summary>
        Task<CommentListItemDTO> UpdateCommentAsync(string commentId, string userId, string content, CancellationToken cancel);

        /// <summary>Xoá bình luận.</summary>
        Task DeleteCommentAsync(string commentId, string userId, CancellationToken cancel);

        /// <summary>Like bình luận.</summary>
        Task<CommentListItemDTO> LikeCommentAsync(string commentId, string userId, CancellationToken cancel);

        /// <summary>Bỏ like bình luận.</summary>
        Task<CommentListItemDTO> UnlikeCommentAsync(string commentId, string userId, CancellationToken cancel);
    }
}
