using Kpett.ChatApp.Constants;
using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Helpers;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Kpett.ChatApp.Services.Implementations
{
    /// <summary>
    /// Service quản lý reaction cho bài viết: thêm, xoá, lấy danh sách reaction.
    /// </summary>
    public class PostReactionService : IPostReactionService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<PostReactionService> _logger;

        private static readonly string ApprovedPostStatus = PostStatus.Approved.GetDescription();
        private const string ActiveStatus = "active";
        private const string DeletedStatus = "deleted";
        private const string PublicPrivacy = "public";
        private const string AdminRole = "admin";
        private const string ModeratorRole = "moderator";

        /// <summary>
        /// Khởi tạo service với database context và logger.
        /// </summary>
        public PostReactionService(AppDbContext dbContext, ILogger<PostReactionService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<PostReactionDTO> AddReactionAsync(string postId, string userId, byte reactionType, CancellationToken cancel)
        {
            if (reactionType == 0)
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Reaction type is required");
            }

            var post = await ApplyPostVisibilityFilter(
                    _dbContext.Posts.Where(p => p.Id == postId && !p.IsDeleted),
                    userId)
                .FirstOrDefaultAsync(cancel);

            if (post == null)
            {
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");
            }

            if (post.Status != null && post.Status != ApprovedPostStatus)
                throw new ForbiddenException(ErrorCodes.POST.USER_NOT_AUTHORIZED, "Cannot react to a post that is not approved");

            _logger.LogInformation("User {UserId} added reaction to post with ID {PostId}", userId, postId);

            var existingReaction = await _dbContext.PostReactions
                .FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == userId);

            var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                if (existingReaction != null)
                {
                    existingReaction.Type = reactionType;
                    existingReaction.CreatedAt = DateTime.UtcNow;
                    _dbContext.PostReactions.Update(existingReaction);
                }
                else
                {
                    var reaction = new PostReaction
                    {
                        Id = Guid.NewGuid().ToString(),
                        PostId = postId,
                        UserId = userId,
                        Type = reactionType,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _dbContext.PostReactions.AddAsync(reaction);

                    await _dbContext.Posts
                            .Where(p => p.Id == postId)
                            .ExecuteUpdateAsync(p => p.SetProperty(x => x.LikeCount, x => x.LikeCount + 1));

                    await transaction.CommitAsync();
                }

                await _dbContext.SaveChangesAsync();

                var updatedReaction = await _dbContext.PostReactions
                    .AsNoTracking()
                    .FirstAsync(r => r.PostId == postId && r.UserId == userId);

                return new PostReactionDTO
                {
                    Id = updatedReaction.Id,
                    PostId = updatedReaction.PostId,
                    UserId = updatedReaction.UserId,
                    Type = updatedReaction.Type,
                    CreatedAt = updatedReaction.CreatedAt
                };
            }
            catch
            {
                await transaction.RollbackAsync(cancel);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task RemoveReactionAsync(string postId, string userId, CancellationToken cancel)
        {
            _logger.LogInformation("User {UserId} is removing reaction from post with ID {PostId}", userId, postId);

            var postExists = await ApplyPostVisibilityFilter(
                    _dbContext.Posts.Where(p => p.Id == postId && !p.IsDeleted),
                    userId)
                .Where(p => p.Status == null || p.Status == ApprovedPostStatus)
                .AnyAsync(cancel);
            if (!postExists)
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");

            var reaction = await _dbContext.PostReactions
                .FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == userId);

            var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                if (reaction != null)
                {
                    _dbContext.PostReactions.Remove(reaction);
                    await _dbContext.SaveChangesAsync();

                    await _dbContext.Posts
                        .Where(p => p.Id == postId)
                        .ExecuteUpdateAsync(p => p.SetProperty(x => x.LikeCount, x => x.LikeCount - 1));

                    await transaction.CommitAsync();
                }
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<PostReactionDTO>> GetPostReactionsAsync(string postId, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            var postExists = await _dbContext.Posts
                .AnyAsync(p => p.Id == postId && !p.IsDeleted, cancel);
            if (!postExists)
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");

            var reactions = await _dbContext.PostReactions
                .AsNoTracking()
                .Where(r => r.PostId == postId)
                .Select(r => new PostReactionDTO
                {
                    Id = r.Id,
                    PostId = r.PostId,
                    UserId = r.UserId,
                    Type = r.Type,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync(cancel);

            return reactions;
        }

        // ─── Private helpers ───

        private IQueryable<Post> ApplyPostVisibilityFilter(IQueryable<Post> query, string? currentUserId)
        {
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return query.Where(p =>
                    p.GroupId == null ||
                    (
                        (p.Status == null || p.Status == ApprovedPostStatus) &&
                        _dbContext.Groups.Any(g =>
                            g.Id == p.GroupId &&
                            (g.Status == null || g.Status != DeletedStatus) &&
                            (g.Type == null || g.Type == PublicPrivacy))
                    ));
            }

            return query.Where(p =>
                p.GroupId == null ||
                (
                    _dbContext.Groups.Any(g =>
                        g.Id == p.GroupId &&
                        (g.Status == null || g.Status != DeletedStatus) &&
                        (
                            g.Type == null ||
                            g.Type == PublicPrivacy ||
                            _dbContext.GroupMembers.Any(m =>
                                m.GroupId == p.GroupId &&
                                m.UserId == currentUserId &&
                                m.Status == ActiveStatus)
                        )) &&
                    (
                        p.Status == null ||
                        p.Status == ApprovedPostStatus ||
                        p.CreatedByUserId == currentUserId ||
                        _dbContext.Groups.Any(g => g.Id == p.GroupId && g.OwnerUserId == currentUserId) ||
                        _dbContext.GroupMembers.Any(m =>
                            m.GroupId == p.GroupId &&
                            m.UserId == currentUserId &&
                            m.Status == ActiveStatus &&
                            (m.Role == AdminRole || m.Role == ModeratorRole))
                    )
                ));
        }
    }
}
