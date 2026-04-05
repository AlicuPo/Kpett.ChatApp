using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Payload.Cursor;
using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Extentions;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using static Kpett.ChatApp.Contants.ErrorCodes;

namespace Kpett.ChatApp.Services.Impls
{
    public class CommentService: ICommentService
    {
        private static readonly Regex MentionTokenRegex = new(
            "<@(?<userId>[^<>\\s]+)>",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly AppDbContext _dbContext;
        public CommentService(AppDbContext dbContext) 
        { 
            _dbContext = dbContext;
        }

        public async Task<CommentListItemDTO> AddCommentAsync(string postId, string userId, string content, string? parentCommentId, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Comment content cannot be empty");
            }

            cancel.ThrowIfCancellationRequested();

            var utcNow = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
            var normalizedParentCommentId = NormalizeOptionalString(parentCommentId);
            var post = await _dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId, cancel);
            if (post == null)
            {
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");

            }

            Comment? parentComment = null;
            if (!string.IsNullOrEmpty(normalizedParentCommentId))
            {
                parentComment = await _dbContext.Comments
                    .FirstOrDefaultAsync(c => c.Id == normalizedParentCommentId && c.PostId == postId && c.DeletedAt == null, cancel);

                if (parentComment == null)
                    throw new NotFoundException(ErrorCodes.COMMENT.PARENT_COMMENT_NOT_FOUND, "Parent comment not found");
            }

            var user = _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .FirstOrDefault();

            if (user == null)
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");

            var comment = new Comment
            {
                Id = Guid.NewGuid().ToString(),
                PostId = postId,
                UserId = userId,
                Content = content,
                ParentCommentId = normalizedParentCommentId,
                LikeCount = 0,
                ReplyCount = 0,
                IsEdited = false,
                CreatedAt = utcNow
            };

            await _dbContext.Comments.AddAsync(comment, cancel);

            if (parentComment != null)
            {
                parentComment.ReplyCount += 1;
            }

            var mentions = await SyncCommentMentionsAsync(comment.Id, ExtractMentionUserIds(content), utcNow, cancel);
            await _dbContext.SaveChangesAsync(cancel);

            return MapCommentListItem(
                    comment,
                    user,
                    mentions,
                    false,
                    user.Id);
        }

        public async Task<PaginatedData<CommentListItemDTO>> GetCommentsAsync(
            string postId,
            string parentCommentId,
            string currentUserId,
            string? cursor,
            int limit,
            CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            var postExists = await _dbContext.Posts
                .AnyAsync(p => p.Id == postId && !p.IsDeleted, cancel);
            if (!postExists)
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");

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
            var totalCount = await _dbContext.Comments
                .AsNoTracking()
                .CountAsync(c => c.PostId == postId && c.DeletedAt == null && c.ParentCommentId == parentCommentId, cancel);

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

            var commentRows = await query
                .OrderBy(c => c.CreatedAt)
                .ThenByDescending(c => c.Id)
                .Take(normalizedLimit + 1)
                .Join(
                    _dbContext.Users.AsNoTracking(),
                    c => c.UserId,
                    u => u.Id,
                    (c, u) => new CommentRow(c, u))
                .ToListAsync(cancel);

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
                    GetMentionSummaries(mentionLookup, x.Comment.Id),
                    likedCommentIds.Contains(x.Comment.Id),
                    currentUserId))
                .ToList();

            return new PaginatedData<CommentListItemDTO>
            {
                Items = items,
                Pagination = new CursorPaginationMeta
                {
                    NextCursor = nextCursor,
                    Limit = normalizedLimit,
                    TotalCount = totalCount
                }
            };
        }

        // Method help post

        // Method help comment

        public async Task<CommentListItemDTO> UpdateCommentAsync(
            string commentId,
            string userId,
            string content,
            CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Comment content cannot be empty");
            }

            cancel.ThrowIfCancellationRequested();

            var comment = await _dbContext.Comments
                .FirstOrDefaultAsync(c => c.Id == commentId && c.DeletedAt == null, cancel);

            if (comment == null)
            {
                throw new NotFoundException(ErrorCodes.COMMENT.NOT_FOUND, "Comment not found");  
            }

            if (comment.UserId != userId)
            {
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
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");
            }

            return MapCommentListItem(
                    comment,
                    user,
                    mentions,
                    false,
                    user.Id);
        }

        public async Task DeleteCommentAsync(string commentId, string userId, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            var comment = await _dbContext.Comments
                .FirstOrDefaultAsync(c => c.Id == commentId && c.DeletedAt == null, cancel);

            if (comment == null)
                throw new NotFoundException(ErrorCodes.COMMENT.NOT_FOUND, "Comment not found");

            if (comment.UserId != userId)
                throw new ForbiddenException(ErrorCodes.COMMENT.USER_NOT_AUTHORIZED, "Not authorized to delete this comment");

            if (!string.IsNullOrEmpty(comment.ParentCommentId))
            {
                var parentComment = await _dbContext.Comments
                    .FirstOrDefaultAsync(c => c.Id == comment.ParentCommentId, cancel);

                if (parentComment != null)
                {
                    parentComment.ReplyCount = Math.Max(0, parentComment.ReplyCount - 1);
                }
            }

            comment.DeletedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancel);
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

        private async Task<Dictionary<string, List<CommentMentionDTO>>> GetCommentMentionsLookupAsync(
            IReadOnlyCollection<string> commentIds,
            CancellationToken cancel)
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
                    AvatarUrl = userInfo.AvatarUrl,
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

                if (existingByUserId.TryGetValue(mentionUserId, out var existingMention))
                {
                    existingMention.Username = snapshot.Username;
                    existingMention.DisplayName = snapshot.DisplayName;
                    existingMention.UpdatedAt = utcNow;
                    continue;
                }

                commentMetions.Add(new CommentMentionSummaryDTO
                {
                    UserId = mentionUserId,
                    Username = snapshot.Username,
                    DisplayName = snapshot.DisplayName,
                });

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
                UserAvatar = userInfo?.AvatarUrl,
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

        private sealed record CommentRow(Comment Comment, User User);
    }
}
