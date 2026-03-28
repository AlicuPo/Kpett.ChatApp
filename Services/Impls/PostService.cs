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
using System.ComponentModel;
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

            string ? nextCursor = null;
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

        /// <summary>
        /// Add a comment to a post
        /// </summary>
        public async Task<CommentDTO> AddCommentAsync(string postId, string userId, string content, string? parentCommentId, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Comment content cannot be empty");

            cancel.ThrowIfCancellationRequested();

            var post = await _dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId, cancel);
            if (post == null)
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");

            // Validate parent comment if provided
            if (!string.IsNullOrEmpty(parentCommentId))
            {
                var parentComment = await _dbContext.Comments
                    .FirstOrDefaultAsync(c => c.Id == parentCommentId && c.PostId == postId && c.DeletedAt == null, cancel);

                if (parentComment == null)
                    throw new NotFoundException(ErrorCodes.POST.PARENT_POST_NOT_FOUND, "Parent comment not found");
            }

            var comment = new Comment
            {
                Id = Guid.NewGuid().ToString(),
                PostId = postId,
                UserId = userId,
                Content = content,
                ParentCommentId = parentCommentId,
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.Comments.AddAsync(comment, cancel);
            await _dbContext.SaveChangesAsync(cancel);

            // Notify post owner
            if (post.CreatedByUserId != userId)
            {
                try
                {
                    var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancel);
                    await _realtimeService.PublishAsync($"user:{post.CreatedByUserId}:notifications", new
                    {
                        type = "POST_COMMENT",
                        postId = postId,
                        commentId = comment.Id,
                        userId = userId,
                        userName = user?.DisplayName ?? user?.Username,
                        timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Real-time notification failed: {ex.Message}");
                }
            }

            var user_info = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancel);

            return new CommentDTO
            {
                Id = comment.Id,
                PostId = comment.PostId,
                UserId = comment.UserId,
                UserName = user_info?.DisplayName ?? user_info?.Username ?? "Anonymous",
                UserAvatar = user_info?.AvatarUrl,
                Content = comment.Content,
                ParentCommentId = comment.ParentCommentId,
                CreatedAt = comment.CreatedAt
            };
        }

        /// <summary>
        /// Get comments on a post with replies
        /// </summary>
        public async Task<List<CommentDTO>> GetCommentsAsync(string postId, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            var postExists = await _dbContext.Posts
                .AnyAsync(p => p.Id == postId && (!p.IsDeleted.HasValue || !p.IsDeleted.Value), cancel);
            if (!postExists)
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");

            var comments = await _dbContext.Comments
                .AsNoTracking()
                .Where(c => c.PostId == postId && c.DeletedAt == null && string.IsNullOrEmpty(c.ParentCommentId))
                .Join(
                    _dbContext.Users,
                    c => c.UserId,
                    u => u.Id,
                    (c, u) => new { Comment = c, User = u }
                )
                .OrderByDescending(x => x.Comment.CreatedAt)
                .ToListAsync(cancel);

            var result = new List<CommentDTO>();

            foreach (var item in comments)
            {
                var replies = await _dbContext.Comments
                    .AsNoTracking()
                    .Where(c => c.ParentCommentId == item.Comment.Id && c.DeletedAt == null)
                    .Join(
                        _dbContext.Users,
                        c => c.UserId,
                        u => u.Id,
                        (c, u) => new { Comment = c, User = u }
                    )
                    .OrderBy(x => x.Comment.CreatedAt)
                    .Select(x => new CommentDTO
                    {
                        Id = x.Comment.Id,
                        PostId = x.Comment.PostId,
                        UserId = x.Comment.UserId,
                        UserName = x.User.DisplayName ?? x.User.Username,
                        UserAvatar = x.User.AvatarUrl,
                        Content = x.Comment.Content,
                        ParentCommentId = x.Comment.ParentCommentId,
                        CreatedAt = x.Comment.CreatedAt,
                        UpdatedAt = x.Comment.UpdatedAt
                    })
                    .ToListAsync(cancel);

                result.Add(new CommentDTO
                {
                    Id = item.Comment.Id,
                    PostId = item.Comment.PostId,
                    UserId = item.Comment.UserId,
                    UserName = item.User.DisplayName ?? item.User.Username,
                    UserAvatar = item.User.AvatarUrl,
                    Content = item.Comment.Content,
                    ParentCommentId = item.Comment.ParentCommentId,
                    CreatedAt = item.Comment.CreatedAt,
                    UpdatedAt = item.Comment.UpdatedAt,
                    RepliesComments = replies
                });
            }

            return result;
        }

        /// <summary>
        /// Update a comment
        /// </summary>
        public async Task<CommentDTO> UpdateCommentAsync(string commentId, string userId, string content, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Comment content cannot be empty");

            cancel.ThrowIfCancellationRequested();

            var comment = await _dbContext.Comments.FirstOrDefaultAsync(c => c.Id == commentId, cancel);

            if (comment == null)
                throw new NotFoundException(ErrorCodes.COMMENT.NOT_FOUND, "Comment not found");

            if (comment.UserId != userId)
                throw new ForbiddenException(ErrorCodes.COMMENT.USER_NOT_AUTHORIZED, "Not authorized to update this comment");

            comment.Content = content;
            comment.UpdatedAt = DateTime.UtcNow;

            _dbContext.Comments.Update(comment);
            await _dbContext.SaveChangesAsync(cancel);

            return await MapCommentAsync(comment, cancel);
        }

        /// <summary>
        /// Delete a comment (soft delete)
        /// </summary>
        public async Task DeleteCommentAsync(string commentId, string userId, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            var comment = await _dbContext.Comments.FirstOrDefaultAsync(c => c.Id == commentId, cancel);

            if (comment == null)
                throw new NotFoundException(ErrorCodes.COMMENT.NOT_FOUND, "Comment not found");

            if (comment.UserId != userId)
                throw new ForbiddenException(ErrorCodes.COMMENT.USER_NOT_AUTHORIZED, "Not authorized to delete this comment");

            comment.DeletedAt = DateTime.UtcNow;

            _dbContext.Comments.Update(comment);
            await _dbContext.SaveChangesAsync(cancel);
        }

        private async Task<CommentDTO> MapCommentAsync(Comment comment, CancellationToken cancel)
        {
            var userInfo = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == comment.UserId, cancel);

            return new CommentDTO
            {
                Id = comment.Id,
                PostId = comment.PostId,
                UserId = comment.UserId,
                UserName = userInfo?.DisplayName ?? userInfo?.Username ?? "Anonymous",
                UserAvatar = userInfo?.AvatarUrl,
                Content = comment.Content,
                ParentCommentId = comment.ParentCommentId,
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt
            };
        }
    }

}
