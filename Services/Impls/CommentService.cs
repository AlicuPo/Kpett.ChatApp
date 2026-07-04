using Azure.Core;
using Kpett.ChatApp.Constants;
using Kpett.ChatApp.DTOs.Payload.Cursor;
using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Events.Comment;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Extensions;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Kpett.ChatApp.Services.Impls
{
    /// <summary>Service quản lý bình luận: thêm, sửa, xoá, like/unlike, lấy danh sách.</summary>
    public class CommentService : ICommentService
    {
        private static readonly Regex MentionTokenRegex = new(
            "<@(?<userId>[^<>\\s]+)>",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly AppDbContext _dbContext;
        private readonly IMediator _mediator;
        private readonly ILogger<CommentService> _logger;
        /// <summary>Khởi tạo service với các dependencies.</summary>
        public CommentService(AppDbContext dbContext, IMediator mediator, ILogger<CommentService> logger)
        {
            _dbContext = dbContext;
            _mediator = mediator;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<CommentListItemDTO> AddCommentAsync(string postId, string userId, string content, string? parentCommentId, CancellationToken cancel)
        {
            _logger.LogInformation("User {UserId} is adding comment to post {PostId}. HasParent: {HasParent}", userId, postId, !string.IsNullOrWhiteSpace(parentCommentId));

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Add comment rejected for user {UserId} on post {PostId} because content is empty", userId, postId);
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Comment content cannot be empty");
            }

            cancel.ThrowIfCancellationRequested();

            var utcNow = DateTime.UtcNow;
            var commentId = Guid.NewGuid().ToString();
            var normalizedParentCommentId = NormalizeOptionalString(parentCommentId);

            var mentionIds = ExtractMentionUserIds(content);

            var user = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .FirstOrDefaultAsync(cancel);

            if (user == null)
            {
                _logger.LogWarning("Add comment rejected because user {UserId} was not found", userId);
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");
            }

            if (await _dbContext.Posts.AnyAsync(p => p.Id == postId && !p.IsDeleted, cancel) == false)
            {
                _logger.LogWarning("Add comment rejected because post {PostId} was not found", postId);
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");
            }

            string? parentPath = null;
            if (!string.IsNullOrEmpty(normalizedParentCommentId))
            {
                parentPath = await _dbContext.Comments
                    .Where(c => c.Id == normalizedParentCommentId && c.PostId == postId && c.DeletedAt == null)
                    .Select(c => c.Path)
                    .FirstOrDefaultAsync(cancel);

                if (parentPath == null)
                {
                    _logger.LogWarning("Add comment rejected because parent comment {ParentCommentId} was not found on post {PostId}", normalizedParentCommentId, postId);
                    throw new NotFoundException(ErrorCodes.COMMENT.PARENT_COMMENT_NOT_FOUND, "Parent comment not found");
                }
            }

            // Khởi tạo Comment Entity
            var comment = new Comment
            {
                Id = commentId,
                PostId = postId,
                UserId = userId,
                Content = content,
                ParentCommentId = normalizedParentCommentId,
                LikeCount = 0,
                ReplyCount = 0,
                IsEdited = false,
                CreatedAt = utcNow,
                Path = parentPath == null ? commentId : $"{parentPath}/{commentId}"
            };

            using var transaction = await _dbContext.Database.BeginTransactionAsync(cancel);

            try
            {
                await _dbContext.Comments.AddAsync(comment, cancel);

                var mentions = await SyncCommentMentionsAsync(comment.Id, mentionIds, utcNow, cancel);

                await _dbContext.Posts
                    .Where(p => p.Id == postId)
                    .ExecuteUpdateAsync(p => p.SetProperty(x => x.CommentCount, x => x.CommentCount + 1));

                if (!string.IsNullOrEmpty(normalizedParentCommentId))
                {
                    await _dbContext.Comments
                        .Where(c => c.Id == normalizedParentCommentId)
                        .ExecuteUpdateAsync(c => c.SetProperty(x => x.ReplyCount, x => x.ReplyCount + 1));
                }

                await _dbContext.SaveChangesAsync(cancel);

                await transaction.CommitAsync(cancel);

                var userMedia = _dbContext.UserMedias
                    .AsNoTracking()
                    .FirstOrDefault(um => um.UserId == userId && um.MediaType == UserMediaType.Avatar.GetDescription() && um.IsPrimary);

                if (mentionIds.Any())
                {
                    // Trích xuất đoạn snippet (50 ký tự) từ Content để hiển thị tóm tắt trên UI Thông báo
                    string snippet = comment.Content.Length > 50
                        ? comment.Content.Substring(0, 50) + "..."
                        : comment.Content;

                    await _mediator.Publish(new CommentMentionedEvent
                    {
                        PostId = comment.PostId,
                        CommentId = comment.Id,
                        ActorId = userId,
                        MentionedUserIds = mentionIds,
                        CommentSnippet = snippet
                    }, cancel);
                }

                _logger.LogInformation("User {UserId} added comment {CommentId} to post {PostId}. MentionCount: {MentionCount}", userId, comment.Id, postId, mentionIds.Count);
                return MapCommentListItem(comment, user, userMedia, mentions, false, user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding comment {CommentId} to post {PostId} by user {UserId}", commentId, postId, userId);
                await transaction.RollbackAsync(cancel);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<PaginatedData<CommentListItemDTO>> GetCommentsAsync(
            string postId,
            string parentCommentId,
            string currentUserId,
            string? cursor,
            int limit,
            CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            _logger.LogInformation("User {UserId} is retrieving comments for post {PostId}. ParentCommentId: {ParentCommentId}", currentUserId, postId, parentCommentId);

            var postExists = await _dbContext.Posts
                .AnyAsync(p => p.Id == postId && !p.IsDeleted, cancel);
            if (!postExists)
            {
                _logger.LogWarning("Get comments rejected because post {PostId} was not found", postId);
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");
            }

            // Giải mã Cursor
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

            var normalizedLimit = limit <= 0 ? 20 : Math.Min(limit, 100);

            var query = _dbContext.Comments
                .AsNoTracking()
                .Where(c => c.PostId == postId && c.DeletedAt == null && c.ParentCommentId == parentCommentId);

            // Áp dụng điều kiện lọc Compound (Date + Id)
            if (cursorDate.HasValue && !string.IsNullOrEmpty(cursorId))
            {
                query = query.Where(c =>
                    c.CreatedAt > cursorDate.Value ||
                    (c.CreatedAt == cursorDate.Value && c.Id.CompareTo(cursorId) < 0));
            }

            var pagedQuery = query
                .OrderBy(c => c.CreatedAt)
                .ThenByDescending(c => c.Id)
                .Take(normalizedLimit + 1);

            var avatars = _dbContext.UserMedias
                .AsNoTracking()
                .Where(m => m.MediaType == UserMediaType.Avatar.GetDescription() && m.IsPrimary);

            var commentRows = await (
                from c in pagedQuery
                join u in _dbContext.Users.AsNoTracking() on c.UserId equals u.Id

                join a in avatars on u.Id equals a.UserId into avatarGroup
                from userAvatar in avatarGroup.DefaultIfEmpty()

                select new CommentRow(c, u, userAvatar)
            ).ToListAsync(cancel);

            var hasMore = commentRows.Count > normalizedLimit;
            var pagedCommentRows = hasMore
                ? commentRows.Take(normalizedLimit).ToList()
                : commentRows;

            // Encode Next Cursor
            string? nextCursor = null;
            if (hasMore)
            {
                var lastItemInPage = pagedCommentRows.Last().Comment;
                nextCursor = CursorHelper.Encode(new BaseCursorPayload
                {
                    Id = lastItemInPage.Id,
                    CreatedAt = lastItemInPage.CreatedAt
                });
            }

            // Các logic lookup giữ nguyên
            var mentionLookup = await GetCommentMentionsLookupAsync(
                pagedCommentRows.Select(x => x.Comment.Id).ToList(),
                cancel);

            var likedCommentIds = await GetLikedCommentIdsAsync(
                pagedCommentRows.Select(x => x.Comment.Id).ToList(),
                currentUserId,
                cancel);

            var items = pagedCommentRows
                .Select(x => MapCommentListItem(
                    x.Comment,
                    x.User,
                    x.UserMedia,
                    GetMentionSummaries(mentionLookup, x.Comment.Id),
                    likedCommentIds.Contains(x.Comment.Id),
                    currentUserId))
                .ToList();

            _logger.LogInformation("User {UserId} retrieved {Count} comments for post {PostId}", currentUserId, items.Count, postId);
            return new PaginatedData<CommentListItemDTO>
            {
                Items = items,
                Pagination = new CursorPaginationMeta
                {
                    NextCursor = nextCursor,
                    Limit = normalizedLimit,
                }
            };
        }

        /// <inheritdoc />
        public async Task<CommentListItemDTO> UpdateCommentAsync(
            string commentId,
            string userId,
            string content,
            CancellationToken cancel)
        {
            _logger.LogInformation("User {UserId} is updating comment {CommentId}", userId, commentId);

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Update comment {CommentId} rejected for user {UserId} because content is empty", commentId, userId);
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Comment content cannot be empty");
            }

            cancel.ThrowIfCancellationRequested();

            var comment = await _dbContext.Comments
                .FirstOrDefaultAsync(c => c.Id == commentId && c.DeletedAt == null, cancel);

            if (comment == null)
            {
                _logger.LogWarning("Update comment rejected because comment {CommentId} was not found", commentId);
                throw new NotFoundException(ErrorCodes.COMMENT.NOT_FOUND, "Comment not found");
            }

            if (comment.UserId != userId)
            {
                _logger.LogWarning("User {UserId} attempted to update unauthorized comment {CommentId}", userId, commentId);
                throw new ForbiddenException(ErrorCodes.COMMENT.USER_NOT_AUTHORIZED, "Not authorized to update this comment");
            }

            var utcNow = DateTime.UtcNow;
            comment.Content = content;
            comment.IsEdited = true;
            comment.UpdatedAt = utcNow;

            var mentions = await SyncCommentMentionsAsync(comment.Id, ExtractMentionUserIds(content), utcNow, cancel);
            await _dbContext.SaveChangesAsync(cancel);

            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == comment.UserId, cancel);

            if (user == null)
            {
                _logger.LogWarning("Update comment {CommentId} failed because author {UserId} was not found", commentId, comment.UserId);
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");
            }

            var userMedia = _dbContext.UserMedias
                                .AsNoTracking()
                                .FirstOrDefault(um => um.UserId == userId && um.MediaType == UserMediaType.Avatar.GetDescription() && um.IsPrimary);

            _logger.LogInformation("User {UserId} updated comment {CommentId}. MentionCount: {MentionCount}", userId, commentId, mentions.Count);
            return MapCommentListItem(comment, user, userMedia, mentions, false, user.Id);
        }

        /// <inheritdoc />
        public async Task DeleteCommentAsync(string commentId, string userId, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            _logger.LogInformation("User {UserId} is deleting comment {CommentId}", userId, commentId);

            var commentInfo = await _dbContext.Comments
                .Where(c => c.Id == commentId && c.DeletedAt == null)
                .Select(c => new { c.UserId, c.ParentCommentId, c.PostId })
                .FirstOrDefaultAsync(cancel);

            if (commentInfo == null)
            {
                _logger.LogWarning("Delete comment rejected because comment {CommentId} was not found", commentId);
                throw new NotFoundException(ErrorCodes.COMMENT.NOT_FOUND, "Comment not found");
            }

            if (commentInfo.UserId != userId)
            {
                _logger.LogWarning("User {UserId} attempted to delete unauthorized comment {CommentId}", userId, commentId);
                throw new ForbiddenException(ErrorCodes.COMMENT.USER_NOT_AUTHORIZED, "Not authorized to delete this comment");
            }

            var utcNow = DateTime.UtcNow;

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancel);
            try
            {
                await _dbContext.Comments
                    .Where(c => c.Id == commentId)
                    .ExecuteUpdateAsync(c => c.SetProperty(x => x.DeletedAt, utcNow), cancel);

                if (!string.IsNullOrEmpty(commentInfo.ParentCommentId))
                {
                    await _dbContext.Comments
                        .Where(c => c.Id == commentInfo.ParentCommentId && c.ReplyCount > 0)
                        .ExecuteUpdateAsync(c => c.SetProperty(x => x.ReplyCount, x => x.ReplyCount - 1), cancel);
                }

                await _dbContext.Posts
                    .Where(p => p.Id == commentInfo.PostId && p.CommentCount > 0)
                    .ExecuteUpdateAsync(p => p.SetProperty(x => x.CommentCount, x => x.CommentCount - 1), cancel);

                await transaction.CommitAsync(cancel);
                _logger.LogInformation("User {UserId} deleted comment {CommentId} from post {PostId}", userId, commentId, commentInfo.PostId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting comment {CommentId} by user {UserId}", commentId, userId);
                await transaction.RollbackAsync(cancel);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<CommentListItemDTO> LikeCommentAsync(string commentId, string userId, CancellationToken cancel)
        {
            _logger.LogInformation("User {UserId} is liking comment {CommentId}", userId, commentId);

            if (string.IsNullOrWhiteSpace(commentId))
            {
                _logger.LogWarning("Like comment rejected for user {UserId} because comment ID is empty", userId);
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Comment ID is required");
            }

            var commentExists = await _dbContext.Comments
                .AnyAsync(c => c.Id == commentId && c.DeletedAt == null, cancel);

            if (!commentExists)
            {
                _logger.LogWarning("Like comment rejected because comment {CommentId} was not found", commentId);
                throw new NotFoundException(ErrorCodes.COMMENT.NOT_FOUND, "Comment not found");
            }

            var alreadyLiked = await _dbContext.CommentLikes
                .AnyAsync(cl => cl.CommentId == commentId && cl.UserId == userId, cancel);

            if (!alreadyLiked)
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancel);
                try
                {
                    await _dbContext.CommentLikes.AddAsync(new CommentLike
                    {
                        Id = Guid.NewGuid().ToString(),
                        CommentId = commentId,
                        UserId = userId,
                        CreatedAt = DateTime.UtcNow
                    }, cancel);

                    await _dbContext.Comments
                        .Where(c => c.Id == commentId)
                        .ExecuteUpdateAsync(c => c.SetProperty(x => x.LikeCount, x => x.LikeCount + 1), cancel);

                    await _dbContext.SaveChangesAsync(cancel);
                    await transaction.CommitAsync(cancel);
                    _logger.LogInformation("User {UserId} liked comment {CommentId}", userId, commentId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error liking comment {CommentId} by user {UserId}", commentId, userId);
                    await transaction.RollbackAsync(cancel);
                    throw;
                }
            }
            else
            {
                _logger.LogDebug("User {UserId} already liked comment {CommentId}", userId, commentId);
            }

            return await MapCommentListItemByIdAsync(commentId, userId, true, cancel);
        }

        /// <inheritdoc />
        public async Task<CommentListItemDTO> UnlikeCommentAsync(string commentId, string userId, CancellationToken cancel)
        {
            _logger.LogInformation("User {UserId} is unliking comment {CommentId}", userId, commentId);

            if (string.IsNullOrWhiteSpace(commentId))
            {
                _logger.LogWarning("Unlike comment rejected for user {UserId} because comment ID is empty", userId);
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Comment ID is required");
            }

            var like = await _dbContext.CommentLikes
                .FirstOrDefaultAsync(cl => cl.CommentId == commentId && cl.UserId == userId, cancel);

            if (like != null)
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancel);
                try
                {
                    _dbContext.CommentLikes.Remove(like);

                    await _dbContext.Comments
                        .Where(c => c.Id == commentId && c.LikeCount > 0)
                        .ExecuteUpdateAsync(c => c.SetProperty(x => x.LikeCount, x => x.LikeCount - 1), cancel);

                    await _dbContext.SaveChangesAsync(cancel);
                    await transaction.CommitAsync(cancel);
                    _logger.LogInformation("User {UserId} unliked comment {CommentId}", userId, commentId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error unliking comment {CommentId} by user {UserId}", commentId, userId);
                    await transaction.RollbackAsync(cancel);
                    throw;
                }
            }
            else
            {
                var commentExists = await _dbContext.Comments
                    .AnyAsync(c => c.Id == commentId && c.DeletedAt == null, cancel);

                if (!commentExists)
                {
                    _logger.LogWarning("Unlike comment rejected because comment {CommentId} was not found", commentId);
                    throw new NotFoundException(ErrorCodes.COMMENT.NOT_FOUND, "Comment not found");
                }
                _logger.LogDebug("User {UserId} had not liked comment {CommentId}", userId, commentId);
            }

            return await MapCommentListItemByIdAsync(commentId, userId, false, cancel);
        }

        private async Task<CommentDTO> MapCommentAsync(Comment comment, CancellationToken cancel)
        {
            var userInfo = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == comment.UserId, cancel);

            var mentionLookup = await GetCommentMentionsLookupAsync(new[] { comment.Id }, cancel);

            return MapComment(
                comment,
                userInfo,
                GetMentions(mentionLookup, comment.Id),
                new List<CommentDTO>());
        }

        private async Task<CommentListItemDTO> MapCommentListItemByIdAsync(
            string commentId,
            string currentUserId,
            bool isLiked,
            CancellationToken cancel)
        {
            var row = await (
                from c in _dbContext.Comments.AsNoTracking()
                join u in _dbContext.Users.AsNoTracking() on c.UserId equals u.Id
                join a in _dbContext.UserMedias.AsNoTracking()
                        .Where(m => m.MediaType == UserMediaType.Avatar.GetDescription() && m.IsPrimary)
                    on u.Id equals a.UserId into avatarGroup
                from userAvatar in avatarGroup.DefaultIfEmpty()
                where c.Id == commentId && c.DeletedAt == null
                select new CommentRow(c, u, userAvatar)
            ).FirstOrDefaultAsync(cancel);

            if (row == null)
            {
                throw new NotFoundException(ErrorCodes.COMMENT.NOT_FOUND, "Comment not found");
            }

            var mentionLookup = await GetCommentMentionsLookupAsync(new[] { commentId }, cancel);

            return MapCommentListItem(
                row.Comment,
                row.User,
                row.UserMedia,
                GetMentionSummaries(mentionLookup, commentId),
                isLiked,
                currentUserId);
        }

        private async Task<Dictionary<string, List<CommentMentionDTO>>> GetCommentMentionsLookupAsync(IReadOnlyCollection<string> commentIds, CancellationToken cancel)
        {
            if (commentIds.Count == 0)
            {
                return new Dictionary<string, List<CommentMentionDTO>>(StringComparer.Ordinal);
            }

            var mentions = await _dbContext.MentionComments
                .AsNoTracking()
                .Where(m => commentIds.Contains(m.CommentId))
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    m.CommentId,
                    Mention = new CommentMentionDTO
                    {
                        Id = m.Id,
                        UserId = m.UserId,
                        Username = m.Username,
                        DisplayName = m.DisplayName,
                        IsNotified = m.IsNotified,
                        CreatedAt = m.CreatedAt,
                        UpdatedAt = m.UpdatedAt
                    }
                })
                .ToListAsync(cancel);

            return mentions
                .GroupBy(x => x.CommentId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(x => x.Mention).ToList(),
                    StringComparer.Ordinal);
        }

        private async Task<HashSet<string>> GetLikedCommentIdsAsync(
            IReadOnlyCollection<string> commentIds,
            string currentUserId,
            CancellationToken cancel)
        {
            if (commentIds.Count == 0 || string.IsNullOrWhiteSpace(currentUserId))
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            var likedCommentIds = await _dbContext.CommentLikes
                .AsNoTracking()
                .Where(cl => cl.UserId == currentUserId && commentIds.Contains(cl.CommentId))
                .Select(cl => cl.CommentId)
                .ToListAsync(cancel);

            return likedCommentIds.ToHashSet(StringComparer.Ordinal);
        }

        private static List<CommentMentionSummaryDTO> GetMentionSummaries(
            IReadOnlyDictionary<string, List<CommentMentionDTO>> mentionLookup,
            string commentId)
        {
            if (!mentionLookup.TryGetValue(commentId, out var mentions))
            {
                return new List<CommentMentionSummaryDTO>();
            }

            return mentions
                .Select(m => new CommentMentionSummaryDTO
                {
                    UserId = m.UserId,
                    Username = m.Username,
                    DisplayName = m.DisplayName
                })
                .ToList();
        }

        private static CommentListItemDTO MapCommentListItem(
            Comment comment,
            User userInfo,
            UserMedia? userMedia,
            List<CommentMentionSummaryDTO> mentions,
            bool isLiked,
            string currentUserId)
        {
            var isDeleted = comment.DeletedAt != null;
            var isOwner = string.Equals(comment.UserId, currentUserId, StringComparison.Ordinal);

            return new CommentListItemDTO
            {
                Id = comment.Id,
                PostId = comment.PostId,
                ParentId = comment.ParentCommentId,
                Author = new CommentAuthorDTO
                {
                    Id = userInfo.Id,
                    Username = userInfo.Username,
                    DisplayName = userInfo.DisplayName,
                    AvatarUrl = userMedia?.MediaUrl,
                    IsVerified = userInfo.IsVerified
                },
                Content = comment.Content,
                Mentions = mentions,
                Attachments = new List<CommentAttachmentDTO>(),
                Metrics = new CommentMetricsDTO
                {
                    LikeCount = comment.LikeCount,
                    ReplyCount = comment.ReplyCount
                },
                ViewerContext = new CommentViewerContextDTO
                {
                    IsLiked = isLiked,
                    CanEdit = !isDeleted && isOwner,
                    CanDelete = !isDeleted && isOwner,
                    CanReply = !isDeleted
                },
                IsEdited = comment.IsEdited,
                IsDeleted = isDeleted,
                CreatedAt = comment.CreatedAt.ToUtc(),
                UpdatedAt = comment.UpdatedAt == null ? null : comment.UpdatedAt.Value.ToUtc()
            };
        }

        private async Task<List<CommentMentionSummaryDTO>> SyncCommentMentionsAsync(
            string commentId,
            IReadOnlyCollection<string> mentionUserIds,
            DateTime utcNow,
            CancellationToken cancel)
        {
            var existingMentions = await _dbContext.MentionComments
                .Where(m => m.CommentId == commentId)
                .ToListAsync(cancel);

            var mentionSnapshots = await LoadMentionUserSnapshotsAsync(mentionUserIds, cancel);
            var requestedUserIds = new HashSet<string>(mentionUserIds, StringComparer.Ordinal);
            var existingByUserId = existingMentions.ToDictionary(m => m.UserId, StringComparer.Ordinal);

            var mentionsToRemove = existingMentions
                .Where(m => !requestedUserIds.Contains(m.UserId))
                .ToList();

            if (mentionsToRemove.Count > 0)
            {
                _dbContext.MentionComments.RemoveRange(mentionsToRemove);
            }

            var commentMetions = new List<CommentMentionSummaryDTO>();

            foreach (var mentionUserId in mentionUserIds)
            {
                var snapshot = mentionSnapshots[mentionUserId];

                commentMetions.Add(new CommentMentionSummaryDTO
                {
                    UserId = mentionUserId,
                    Username = snapshot.Username,
                    DisplayName = snapshot.DisplayName,
                });

                if (existingByUserId.TryGetValue(mentionUserId, out var existingMention))
                {
                    existingMention.Username = snapshot.Username;
                    existingMention.DisplayName = snapshot.DisplayName;
                    existingMention.UpdatedAt = utcNow;
                    continue;
                }

                await _dbContext.MentionComments.AddAsync(new MentionComment
                {
                    Id = Guid.NewGuid().ToString(),
                    CommentId = commentId,
                    UserId = mentionUserId,
                    Username = snapshot.Username,
                    DisplayName = snapshot.DisplayName,
                    IsNotified = false,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                }, cancel);
            }

            return commentMetions;
        }

        private async Task<Dictionary<string, MentionUserSnapshot>> LoadMentionUserSnapshotsAsync(
            IReadOnlyCollection<string> mentionUserIds,
            CancellationToken cancel)
        {
            if (mentionUserIds.Count == 0)
            {
                return new Dictionary<string, MentionUserSnapshot>(StringComparer.Ordinal);
            }

            var users = await _dbContext.Users
                .AsNoTracking()
                .Where(u => mentionUserIds.Contains(u.Id))
                .Select(u => new MentionUserSnapshot(
                    u.Id,
                    u.Username ?? u.DisplayName ?? u.Id,
                    u.DisplayName))
                .ToListAsync(cancel);

            if (users.Count != mentionUserIds.Count)
            {
                var foundUserIds = users.Select(u => u.UserId).ToHashSet(StringComparer.Ordinal);
                var missingUserIds = mentionUserIds
                    .Where(id => !foundUserIds.Contains(id))
                    .ToList();

                throw new BadRequestException(
                    ErrorCodes.VALIDATION.REQUIRED,
                    $"Mentioned users not found: {string.Join(", ", missingUserIds)}");
            }

            return users.ToDictionary(u => u.UserId, StringComparer.Ordinal);
        }

        private static List<string> ExtractMentionUserIds(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return new List<string>();
            }

            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (Match match in MentionTokenRegex.Matches(content))
            {
                var mentionUserId = match.Groups["userId"].Value.Trim();
                if (string.IsNullOrWhiteSpace(mentionUserId))
                {
                    continue;
                }

                if (seen.Add(mentionUserId))
                {
                    result.Add(mentionUserId);
                }
            }

            return result;
        }

        private static string? NormalizeOptionalString(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private static List<CommentMentionDTO> GetMentions(
            IReadOnlyDictionary<string, List<CommentMentionDTO>> mentionLookup,
            string commentId)
        {
            return mentionLookup.TryGetValue(commentId, out var mentions)
                ? mentions
                : new List<CommentMentionDTO>();
        }

        private static CommentDTO MapComment(
            Comment comment,
            User? userInfo,
            List<CommentMentionDTO> mentions,
            List<CommentDTO> replies)
        {
            return new CommentDTO
            {
                Id = comment.Id,
                PostId = comment.PostId,
                UserId = comment.UserId,
                UserName = userInfo?.DisplayName ?? userInfo?.Username ?? "Anonymous",
                Content = comment.Content,
                ParentCommentId = comment.ParentCommentId,
                LikeCount = comment.LikeCount,
                ReplyCount = comment.ReplyCount,
                IsEdited = comment.IsEdited,
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt,
                Mentions = mentions,
                RepliesComments = replies
            };
        }

        private sealed record MentionUserSnapshot(string UserId, string Username, string? DisplayName);

        private sealed record CommentRow(Comment Comment, User User, UserMedia UserMedia);
    }
}

