using Kpett.ChatApp.Constants;
using Kpett.ChatApp.Data;
using Kpett.ChatApp.DTOs.Payload.Cursor;
using Kpett.ChatApp.DTOs.Response.Media;
using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.DTOs.Response.User;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Extensions;
using Kpett.ChatApp.Helpers;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Kpett.ChatApp.Services.Implementations;

public class SavedPostService : ISavedPostService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SavedPostService> _logger;
    private static readonly string PostType = Enums.PostType.Post.GetDescription();
    private static readonly string ReelType = Enums.PostType.Reel.GetDescription();
    private readonly string avatarType = UserMediaType.Avatar.GetDescription();
    private static readonly string ApprovedPostStatus = "approved";

    public SavedPostService(AppDbContext dbContext, ILogger<SavedPostService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SavePostAsync(string userId, string postId, CancellationToken cancel)
    {
        var post = await _dbContext.Posts
            .FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted, cancel)
            ?? throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");

        var existing = await _dbContext.SavedPosts
            .FirstOrDefaultAsync(s => s.UserId == userId && s.PostId == postId, cancel);

        if (existing != null)
            throw new ConflictException(ErrorCodes.POST.ALREADY_SAVED, "Post already saved");

        var saved = new SavedPost
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            PostId = postId,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.SavedPosts.AddAsync(saved, cancel);
        await _dbContext.SaveChangesAsync(cancel);

        _logger.LogInformation("User {UserId} saved post {PostId}", userId, postId);
    }

    public async Task UnsavePostAsync(string userId, string postId, CancellationToken cancel)
    {
        var saved = await _dbContext.SavedPosts
            .FirstOrDefaultAsync(s => s.UserId == userId && s.PostId == postId, cancel);

        if (saved == null)
            throw new NotFoundException(ErrorCodes.POST.NOT_SAVED, "Post not saved");

        _dbContext.SavedPosts.Remove(saved);
        await _dbContext.SaveChangesAsync(cancel);

        _logger.LogInformation("User {UserId} unsaved post {PostId}", userId, postId);
    }

    public async Task<bool> IsPostSavedAsync(string userId, string postId, CancellationToken cancel)
    {
        return await _dbContext.SavedPosts
            .AnyAsync(s => s.UserId == userId && s.PostId == postId, cancel);
    }

    public async Task<PaginatedData<PostThumbnailResponse>> GetSavedPostsAsync(
        string userId, string? currentUserId, string? cursor, int limit, CancellationToken cancel)
    {
        limit = Math.Clamp(limit, 1, 50);

        DateTime? cursorDate = null;
        string? cursorId = null;

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            var decoded = CursorHelper.Decode<BaseCursorPayload>(cursor);
            if (decoded != null)
            {
                cursorDate = decoded.CreatedAt;
                cursorId = decoded.Id;
            }
        }

        var query = _dbContext.SavedPosts
            .AsNoTracking()
            .Where(s => s.UserId == userId);

        if (cursorDate.HasValue && !string.IsNullOrEmpty(cursorId))
        {
            query = query.Where(s =>
                s.CreatedAt < cursorDate.Value ||
                (s.CreatedAt == cursorDate.Value && string.Compare(s.Id, cursorId) < 0));
        }

        var savedData = await query
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .Take(limit + 1)
            .Select(s => new
            {
                s.Id,
                s.PostId,
                s.CreatedAt
            })
            .ToListAsync(cancel);

        if (!savedData.Any())
        {
            return new PaginatedData<PostThumbnailResponse>
            {
                Items = new List<PostThumbnailResponse>(),
                Pagination = new CursorPaginationMeta { Limit = limit }
            };
        }

        string? nextCursor = null;
        var itemsToProcess = savedData;

        if (savedData.Count > limit)
        {
            var lastItem = savedData[limit - 1];
            nextCursor = CursorHelper.Encode(new BaseCursorPayload
            {
                Id = lastItem.Id,
                CreatedAt = lastItem.CreatedAt
            });
            itemsToProcess = savedData.Take(limit).ToList();
        }

        var postIds = itemsToProcess.Select(s => s.PostId).ToList();

        var posts = await _dbContext.Posts
            .AsNoTracking()
            .Where(p => postIds.Contains(p.Id) && !p.IsDeleted)
            .ToDictionaryAsync(p => p.Id, cancel);

        var mediaRows = await _dbContext.PostMedia
            .AsNoTracking()
            .Where(m => m.PostId != null && postIds.Contains(m.PostId))
            .GroupBy(m => m.PostId!)
            .Select(g => new { PostId = g.Key, Media = g.OrderBy(m => m.CreatedAt).First() })
            .ToDictionaryAsync(x => x.PostId, x => x.Media, cancel);

        var likeCounts = await _dbContext.PostReactions
            .AsNoTracking()
            .Where(r => postIds.Contains(r.PostId))
            .GroupBy(r => r.PostId)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), cancel);

        var commentCounts = await _dbContext.Comments
            .AsNoTracking()
            .Where(c => postIds.Contains(c.PostId) && c.DeletedAt == null)
            .GroupBy(c => c.PostId)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), cancel);

        var likedPostIds = string.IsNullOrWhiteSpace(currentUserId)
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await _dbContext.PostReactions
                .AsNoTracking()
                .Where(r => r.UserId == currentUserId && postIds.Contains(r.PostId))
                .Select(r => r.PostId)
                .Distinct()
                .ToListAsync(cancel))
            .ToHashSet(StringComparer.Ordinal);

        var result = new List<PostThumbnailResponse>();

        foreach (var item in itemsToProcess)
        {
            if (!posts.TryGetValue(item.PostId, out var post)) continue;

            var author = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == post.CreatedByUserId)
                .Select(u => new UserResponse
                {
                    Id = u.Id,
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                    AvatarUrl = _dbContext.UserMedias
                        .Where(um => um.UserId == u.Id && um.MediaType == avatarType && um.IsPrimary)
                        .Select(um => um.MediaUrl)
                        .FirstOrDefault(),
                    IsVerified = u.IsVerified,
                })
                .FirstOrDefaultAsync(cancel);

            mediaRows.TryGetValue(item.PostId, out var thumbMedia);

            result.Add(new PostThumbnailResponse
            {
                Id = post.Id,
                Content = post.Content,
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt,
                Privacy = post.Privacy,
                Type = post.Type,
                Status = post.Status ?? ApprovedPostStatus,
                Author = author ?? new UserResponse { Id = post.CreatedByUserId },
                MediaThumbnail = thumbMedia != null ? new MediaPostResponse
                {
                    PublicId = thumbMedia.Id,
                    Url = thumbMedia.MediaUrl,
                    Type = thumbMedia.MediaType
                } : null,
                Metrics = new PostMetricsResponse
                {
                    LikeCount = likeCounts.GetValueOrDefault(item.PostId, 0),
                    CommentCount = commentCounts.GetValueOrDefault(item.PostId, 0)
                },
                ViewerContext = new PostViewerContextResponse
                {
                    IsOwner = post.CreatedByUserId == currentUserId,
                    IsLiked = likedPostIds.Contains(item.PostId),
                    IsSaved = true,
                    CanEdit = false,
                    CanDelete = false,
                    CanLike = true,
                    CanComment = post.AllowComments,
                }
            });
        }

        return new PaginatedData<PostThumbnailResponse>
        {
            Items = result,
            Pagination = new CursorPaginationMeta
            {
                NextCursor = nextCursor,
                Limit = limit
            }
        };
    }
}
