using Kpett.ChatApp.DTOs.Response.Post;

namespace Kpett.ChatApp.Services.Abstractions
{
    /// <summary>
    /// Service quản lý reaction cho bài viết: thêm, xoá, lấy danh sách reaction.
    /// </summary>
    public interface IPostReactionService
    {
        /// <summary>Thêm reaction vào bài viết (hoặc cập nhật nếu đã có).</summary>
        Task<PostReactionDTO> AddReactionAsync(string postId, string userId, byte reactionType, CancellationToken cancel);

        /// <summary>Xoá reaction khỏi bài viết.</summary>
        Task RemoveReactionAsync(string postId, string userId, CancellationToken cancel);

        /// <summary>Lấy danh sách reaction của bài viết.</summary>
        Task<List<PostReactionDTO>> GetPostReactionsAsync(string postId, CancellationToken cancel);
    }
}


