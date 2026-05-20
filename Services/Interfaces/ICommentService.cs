using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface ICommentService
    {
        Task<CommentListItemDTO> AddCommentAsync(string postId, string userId, string content, string? parentCommentId, CancellationToken cancel);
        Task<PaginatedData<CommentListItemDTO>> GetCommentsAsync(string postId, string parentCommentId, string currentUserId, string? cursor, int limit, CancellationToken cancel);
        Task<CommentListItemDTO> UpdateCommentAsync(string commentId, string userId, string content, CancellationToken cancel);
        Task DeleteCommentAsync(string commentId, string userId, CancellationToken cancel);
        Task<CommentListItemDTO> LikeCommentAsync(string commentId, string userId, CancellationToken cancel);
        Task<CommentListItemDTO> UnlikeCommentAsync(string commentId, string userId, CancellationToken cancel);

    }
}
