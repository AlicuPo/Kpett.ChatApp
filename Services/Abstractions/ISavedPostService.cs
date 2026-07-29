using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;

namespace Kpett.ChatApp.Services.Abstractions;

public interface ISavedPostService
{
    Task SavePostAsync(string userId, string postId, CancellationToken cancel);
    Task UnsavePostAsync(string userId, string postId, CancellationToken cancel);
    Task<bool> IsPostSavedAsync(string userId, string postId, CancellationToken cancel);
    Task<PaginatedData<PostThumbnailResponse>> GetSavedPostsAsync(string userId, string? currentUserId, string? cursor, int limit, CancellationToken cancel);
}
