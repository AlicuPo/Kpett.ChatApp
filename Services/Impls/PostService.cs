using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Request.Post;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Media;
using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.DTOs.Response.User;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Receive;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Kpett.ChatApp.Services.Impls
{
    public class PostService : IPostService
    {
        private readonly AppDbContext _dbContext;
        private readonly IRealtimeService _realtimeService;
        private readonly INotificationService _notificationService;
        public PostService(AppDbContext dbContext, IRealtimeService realtimeService, INotificationService notificationService)
        {
            _dbContext = dbContext;
            _realtimeService = realtimeService;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Create a new post with optional media
        /// </summary>
        public async Task<string> CreatePostAsync(string userId, PostRequest postRequest, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "User ID cannot be empty");

            cancel.ThrowIfCancellationRequested();

            // Check if user exists
            var userExists = await _dbContext.Users.AnyAsync(u => u.Id == userId, cancel);
            if (!userExists)
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");

            // Create post
            var newPost = new Post
            {
                Id = Guid.NewGuid().ToString(),
                CreatedByUserId = userId,
                Content = postRequest.Content,
                Privacy = postRequest.Privacy ?? "Public",
                GroupId = postRequest.GroupId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await _dbContext.Posts.AddAsync(newPost, cancel);

            var media = postRequest.Media;

            foreach (var mediaItem in media ?? [])
            {
                var postMedia = new PostMedia
                {
                    Id = mediaItem.PublicId,
                    PostId = newPost.Id,
                    MediaUrl = mediaItem.SecureUrl,
                    MediaType = mediaItem.Type,
                };
                await _dbContext.PostMedia.AddAsync(postMedia, cancel);
            }

            await _dbContext.SaveChangesAsync();

            return newPost.Id;
        }

        /// <summary>
        /// Get a single post with details
        /// </summary>
        public async Task<PostResponseDTO> GetPostAsync(string postId, string? currentUserId, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            var post = await _dbContext.Posts
                .AsNoTracking()
                .Where(p => p.Id == postId && (!p.IsDeleted.HasValue || !p.IsDeleted.Value))
                .Join(
                    _dbContext.Users,
                    p => p.CreatedByUserId,
                    u => u.Id,
                    (p, u) => new { Post = p, User = u }
                )
                .FirstOrDefaultAsync(cancel);

            if (post == null)
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");

            // Get media
            var media = await _dbContext.PostMedia
                .AsNoTracking()
                .Where(m => m.PostId == postId)
                .Select(m => new PostMediaDTO
                {
                    Id = m.Id,
                    PostId = m.PostId,
                    MediaUrl = m.MediaUrl,
                    MediaType = m.MediaType,
                    ThumbnailUrl = m.ThumbnailUrl,
                    Width = m.Width,
                    Height = m.Height,
                    Duration = m.Duration,
                    SortOrder = m.SortOrder
                })
                .ToListAsync(cancel);

            // Get reaction counts
            var likeCount = await _dbContext.PostReactions
                .AsNoTracking()
                .CountAsync(r => r.PostId == postId, cancel);

            var isLiked = false;
            if (!string.IsNullOrEmpty(currentUserId))
            {
                isLiked = await _dbContext.PostReactions
                    .AsNoTracking()
                    .AnyAsync(r => r.PostId == postId && r.UserId == currentUserId, cancel);
            }

            // Get comment count
            var commentCount = await _dbContext.Comments
                .AsNoTracking()
                .CountAsync(c => c.PostId == postId && c.DeletedAt == null && string.IsNullOrEmpty(c.ParentCommentId), cancel);

            return new PostResponseDTO
            {
                Id = post.Post.Id,
                CreatedByUserId = post.Post.CreatedByUserId,
                CreatedByName = post.User.DisplayName ?? post.User.Username,
                CreatedByAvatar = post.User.AvatarUrl,
                Content = post.Post.Content,
                Privacy = post.Post.Privacy,
                GroupId = post.Post.GroupId,
                CreatedAt = post.Post.CreatedAt,
                UpdatedAt = post.Post.UpdatedAt,
                Media = media,
                LikeCount = likeCount,
                CommentCount = commentCount,
                IsLikedByCurrentUser = isLiked
            };
        }

        /// <summary>
        /// Get user feed with pagination
        /// </summary>
        public async Task<PaginatedData<PostFeedResponse>> GetFeedAsync(string currentUserId, string? cursor, int limit = 10)
        {
            // Giải mã Cursor
            DateTime? cursorDate = null;
            string? cursorId = null;

            if (!string.IsNullOrWhiteSpace(cursor))
            {
                var decoded = DecodeCursor(cursor);
                if (decoded != null)
                {
                    cursorDate = decoded.Value.Date;
                    cursorId = decoded.Value.Id;
                }
            }

            var query = _dbContext.Posts.AsNoTracking();

            if (cursorDate.HasValue && !string.IsNullOrEmpty(cursorId))
            {
                query = query.Where(p =>
                    p.CreatedAt < cursorDate.Value ||
                    (p.CreatedAt == cursorDate.Value && p.Id.CompareTo(cursorId) < 0));
            }

            var rawData = await query
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Take(limit + 1)
                .Select(p => new
                {
                    Post = p,
                    Author = _dbContext.Users.Where(u => u.Id == p.CreatedByUserId)
                                               .Select(u => new
                                               {
                                                   Id = u.Id,
                                                   Email = u.Email,
                                                   Username = u.Username,
                                                   DisplayName = u.DisplayName,
                                                   IsVerified = u.IsVerified,
                                                   AvatarUrl = u.AvatarUrl,
                                               })
                                               .FirstOrDefault(),

                    Medias = _dbContext.PostMedia.Where(m => m.PostId == p.Id)
                                         .Select(m => new { m.Id, m.MediaUrl, m.MediaType })
                                         .ToList(),

                    LikeCount = _dbContext.PostReactions.Count(r => r.PostId == p.Id),
                    CommentCount = _dbContext.Comments.Count(c => c.PostId == p.Id),
                    IsLiked = _dbContext.PostReactions.Any(r => r.PostId == p.Id && r.UserId == currentUserId)
                })
                .ToListAsync();

            if (!rawData.Any())
            {
                return new PaginatedData<PostFeedResponse>
                {
                    Items = new List<PostFeedResponse>(),
                    Pagination = new CursorPaginationMeta { Limit = limit }
                };
            }

            string? nextCursor = null;
            var itemsToProcess = rawData;

            if (rawData.Count > limit)
            {
                var lastItemInPage = rawData[limit - 1].Post;
                nextCursor = EncodeCursor(lastItemInPage.CreatedAt ?? DateTime.Now, lastItemInPage.Id);
                itemsToProcess = rawData.Take(limit).ToList();
            }

            var mappedPosts = itemsToProcess.Select(data => new PostFeedResponse
            {
                Id = data.Post.Id,
                Content = data.Post.Content,
                CreatedAt = data.Post.CreatedAt ?? DateTime.Now,
                UpdatedAt = data.Post.UpdatedAt,
                Privacy = data.Post.Privacy,

                Author = data.Author != null
            ? new UserResponse
            {
                Id = data.Author.Id,
                Username = data.Author.Username,
                AvatarUrl = data.Author.AvatarUrl,
                DisplayName = data.Author.DisplayName,
                Email = data.Author.Email,
                IsVerified = data.Author.IsVerified,
            }
            : new UserResponse { Id = data.Post.CreatedByUserId, Username = "Unknown User" },

                Media = data.Medias.Select(m => new MediaPostResponse
                {
                    Id = m.Id,
                    Url = m.MediaUrl,
                    Type = m.MediaType
                }).ToList(),

                Metrics = new PostMetricsResponse
                {
                    LikeCount = data.LikeCount,
                    CommentCount = data.CommentCount
                },

                ViewerContext = new PostViewerContextResponse
                {
                    IsOwner = data.Post.CreatedByUserId == currentUserId,
                    IsLiked = data.IsLiked,
                    IsSaved = false,
                    IsPinned = false,
                    CanEdit = data.Post.CreatedByUserId == currentUserId,
                    CanDelete = data.Post.CreatedByUserId == currentUserId,
                    CanLike = true,
                    CanComment = true,
                    CanPin = data.Post.CreatedByUserId == currentUserId
                }
            }).ToList();

            return new PaginatedData<PostFeedResponse>
            {
                Items = mappedPosts,
                Pagination = new CursorPaginationMeta
                {
                    NextCursor = nextCursor,
                    Limit = limit
                }
            };
        }

        private string EncodeCursor(DateTime createdAt, string id)
        {
            var plainText = $"{createdAt.Ticks}_{id}";
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }

        private (DateTime Date, string Id)? DecodeCursor(string cursor)
        {
            try
            {
                var base64EncodedBytes = Convert.FromBase64String(cursor);
                var plainText = Encoding.UTF8.GetString(base64EncodedBytes);
                var parts = plainText.Split('_', 2);

                if (parts.Length == 2 && long.TryParse(parts[0], out long ticks))
                {
                    var date = new DateTime(ticks);
                    var id = parts[1];
                    return (date, id);
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get all posts from a user
        /// </summary>
        public async Task<List<PostResponseDTO>> GetUserPostsAsync(string userId, SearchRequest request, CancellationToken cancel = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "User ID cannot be empty");

            var userExists = await _dbContext.Users.AnyAsync(u => u.Id == userId, cancel);
            if (!userExists)
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");

            cancel.ThrowIfCancellationRequested();

            var skip = (request.Page - 1) * request.PageSize;

            var posts = await _dbContext.Posts
                .AsNoTracking()
                .Where(p => p.CreatedByUserId == userId && (!p.IsDeleted.HasValue || !p.IsDeleted.Value))
                .OrderByDescending(p => p.CreatedAt)
                .Skip(skip)
                .Take(request.PageSize)
                .Select(p => p.Id)
                .ToListAsync(cancel);

            var result = new List<PostResponseDTO>();

            foreach (var postId in posts)
            {
                var post = await GetPostAsync(postId, userId, cancel);
                result.Add(post);
            }

            return result;
        }

        /// <summary>
        /// Update a post
        /// </summary>
        public async Task<PostResponseDTO> UpdatePostAsync(string postId, string userId, string content, string privacy, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Content cannot be empty");

            cancel.ThrowIfCancellationRequested();

            var post = await _dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId, cancel);

            if (post == null)
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");

            if (post.CreatedByUserId != userId)
                throw new ForbiddenException(ErrorCodes.POST.USER_NOT_AUTHORIZED, "Not authorized to update this post");

            post.Content = content;
            post.Privacy = privacy ?? "Public";
            post.UpdatedAt = DateTime.UtcNow;

            _dbContext.Posts.Update(post);
            await _dbContext.SaveChangesAsync(cancel);

            return await GetPostAsync(postId, userId, cancel);
        }

        /// <summary>
        /// Delete a post (soft delete)
        /// </summary>
        public async Task DeletePostAsync(string postId, string userId, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            var post = await _dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId, cancel);

            if (post == null)
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");

            if (post.CreatedByUserId != userId)
                throw new ForbiddenException(ErrorCodes.POST.USER_NOT_AUTHORIZED, "Not authorized to delete this post");

            post.IsDeleted = true;
            post.UpdatedAt = DateTime.UtcNow;

            _dbContext.Posts.Update(post);
            await _dbContext.SaveChangesAsync(cancel);
        }

        /// <summary>
        /// Add a reaction to a post
        /// </summary>
        public async Task<PostReactionDTO> AddReactionAsync(string postId, string userId, byte reactionType, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            if (reactionType == 0)
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Reaction type is required");

            var post = await _dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId, cancel);
            if (post == null)
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");

            // Check if already reacted
            var existingReaction = await _dbContext.PostReactions
                .FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == userId, cancel);

            if (existingReaction != null)
            {
                // Update existing reaction
                existingReaction.Type = reactionType;
                existingReaction.CreatedAt = DateTime.UtcNow;
                _dbContext.PostReactions.Update(existingReaction);
            }
            else
            {
                // Create new reaction
                var reaction = new PostReaction
                {
                    PostId = postId,
                    UserId = userId,
                    Type = reactionType,
                    CreatedAt = DateTime.UtcNow
                };
                await _dbContext.PostReactions.AddAsync(reaction, cancel);
            }

            await _dbContext.SaveChangesAsync(cancel);

            // Notify post owner
            if (post.CreatedByUserId != userId)
            {
                try
                {
                    var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancel);
                    await _realtimeService.PublishAsync($"user:{post.CreatedByUserId}:notifications", new
                    {
                        type = "POST_REACTION",
                        postId = postId,
                        userId = userId,
                        userName = user?.DisplayName ?? user?.Username,
                        reactionType = reactionType,
                        timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Real-time notification failed: {ex.Message}");
                }
            }

            var updatedReaction = await _dbContext.PostReactions
                .AsNoTracking()
                .FirstAsync(r => r.PostId == postId && r.UserId == userId, cancel);

            return new PostReactionDTO
            {
                Id = updatedReaction.Id,
                PostId = updatedReaction.PostId,
                UserId = updatedReaction.UserId,
                Type = updatedReaction.Type,
                CreatedAt = updatedReaction.CreatedAt
            };
        }

        /// <summary>
        /// Remove a reaction from a post
        /// </summary>
        public async Task RemoveReactionAsync(string postId, string userId, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            var postExists = await _dbContext.Posts
                .AnyAsync(p => p.Id == postId && (!p.IsDeleted.HasValue || !p.IsDeleted.Value), cancel);
            if (!postExists)
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");

            var reaction = await _dbContext.PostReactions
                .FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == userId, cancel);

            if (reaction != null)
            {
                _dbContext.PostReactions.Remove(reaction);
                await _dbContext.SaveChangesAsync(cancel);
            }
        }

        /// <summary>
        /// Get all reactions on a post
        /// </summary>
        public async Task<List<PostReactionDTO>> GetPostReactionsAsync(string postId, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            var postExists = await _dbContext.Posts
                .AnyAsync(p => p.Id == postId && (!p.IsDeleted.HasValue || !p.IsDeleted.Value), cancel);
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

        public async Task<CommentDTO> AddCommentAsync(
            string postId,
            string userId,
            string content,
            string? parentCommentId,
            IEnumerable<string>? mentions,
            CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Comment content cannot be empty");

            cancel.ThrowIfCancellationRequested();

            var utcNow = DateTime.UtcNow;
            var post = await _dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId, cancel);
            if (post == null)
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");

            Comment? parentComment = null;
            if (!string.IsNullOrEmpty(parentCommentId))
            {
                parentComment = await _dbContext.Comments
                    .FirstOrDefaultAsync(c => c.Id == parentCommentId && c.PostId == postId && c.DeletedAt == null, cancel);

                if (parentComment == null)
                    throw new NotFoundException(ErrorCodes.COMMENT.PARENT_COMMENT_NOT_FOUND, "Parent comment not found");
            }

            var comment = new Comment
            {
                Id = Guid.NewGuid().ToString(),
                PostId = postId,
                UserId = userId,
                Content = content,
                ParentCommentId = parentCommentId,
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

            await SyncCommentMentionsAsync(comment.Id, NormalizeMentionUserIds(mentions), utcNow, cancel);
            await _dbContext.SaveChangesAsync(cancel);

            if (post.CreatedByUserId != userId)
            {
                try
                {
                    var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancel);
                    await _realtimeService.PublishAsync($"user:{post.CreatedByUserId}:notifications", new
                    {
                        type = "POST_COMMENT",
                        postId,
                        commentId = comment.Id,
                        userId,
                        userName = user?.DisplayName ?? user?.Username,
                        timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Real-time notification failed: {ex.Message}");
                }
            }

            return await MapCommentAsync(comment, cancel);
        }

        public async Task<CommentsPageDTO> GetCommentsAsync(
            string postId,
            string currentUserId,
            DateTime? cursor,
            int limit,
            CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            var postExists = await _dbContext.Posts
                .AnyAsync(p => p.Id == postId && (!p.IsDeleted.HasValue || !p.IsDeleted.Value), cancel);
            if (!postExists)
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");

            var normalizedLimit = limit <= 0 ? 20 : Math.Min(limit, 100);
            var totalCount = await _dbContext.Comments
                .AsNoTracking()
                .CountAsync(c => c.PostId == postId && c.DeletedAt == null && string.IsNullOrEmpty(c.ParentCommentId), cancel);

            var query = _dbContext.Comments
                .AsNoTracking()
                .Where(c => c.PostId == postId && c.DeletedAt == null && string.IsNullOrEmpty(c.ParentCommentId));

            if (cursor.HasValue)
            {
                query = query.Where(c => c.CreatedAt.HasValue && c.CreatedAt.Value < cursor.Value);
            }

            var commentRows = await query
                .OrderByDescending(c => c.CreatedAt)
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

            return new CommentsPageDTO
            {
                Items = items,
                Pagination = new CommentPaginationDTO
                {
                    NextCursor = hasMore ? items.Last().CreatedAt?.ToString("O") : null,
                    HasMore = hasMore,
                    Limit = normalizedLimit,
                    TotalCount = totalCount
                }
            };
        }

        public async Task<CommentDTO> UpdateCommentAsync(
            string commentId,
            string userId,
            string content,
            IEnumerable<string>? mentions,
            CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Comment content cannot be empty");

            cancel.ThrowIfCancellationRequested();

            var comment = await _dbContext.Comments
                .FirstOrDefaultAsync(c => c.Id == commentId && c.DeletedAt == null, cancel);

            if (comment == null)
                throw new NotFoundException(ErrorCodes.COMMENT.NOT_FOUND, "Comment not found");

            if (comment.UserId != userId)
                throw new ForbiddenException(ErrorCodes.COMMENT.USER_NOT_AUTHORIZED, "Not authorized to update this comment");

            var utcNow = DateTime.UtcNow;
            comment.Content = content;
            comment.IsEdited = true;
            comment.UpdatedAt = utcNow;

            await SyncCommentMentionsAsync(comment.Id, NormalizeMentionUserIds(mentions), utcNow, cancel);
            await _dbContext.SaveChangesAsync(cancel);

            return await MapCommentAsync(comment, cancel);
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
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt
            };
        }

        private async Task SyncCommentMentionsAsync(
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

        private static List<string> NormalizeMentionUserIds(IEnumerable<string>? mentions)
        {
            if (mentions == null)
            {
                return new List<string>();
            }

            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var mention in mentions)
            {
                if (string.IsNullOrWhiteSpace(mention))
                {
                    continue;
                }

                var normalizedMention = mention.Trim();
                if (seen.Add(normalizedMention))
                {
                    result.Add(normalizedMention);
                }
            }

            return result;
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
