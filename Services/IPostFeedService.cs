using Azure.Core;
using CloudinaryDotNet;
using Kpett.ChatApp.DTOs;
using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Receive;
using Microsoft.EntityFrameworkCore;

namespace Kpett.ChatApp.Services
{
    public interface IPostFeedService
    {
        // Post operations
        Task PostFeed (PostMediaRequest postRequest, CancellationToken cancel);
        Task<PostResponseDTO> CreatePostAsync(string userId, PostMediaRequest postRequest, CancellationToken cancel);
        Task<PostResponseDTO> GetPostAsync(long postId, string? currentUserId, CancellationToken cancel);
        Task<List<UserFeedDTO>> GetUserFeedAsync(string userId, SearchRequest request, CancellationToken cancel = default);
        Task<List<PostResponseDTO>> GetUserPostsAsync(string userId,SearchRequest request, CancellationToken cancel = default);
        Task UpdatePostAsync(long postId, string userId, string content, string privacy, CancellationToken cancel);
        Task DeletePostAsync(long postId, string userId, CancellationToken cancel);

        // Reaction operations
        Task<PostReactionDTO> AddReactionAsync(long postId, string userId, byte reactionType, CancellationToken cancel);
        Task RemoveReactionAsync(long postId, string userId, CancellationToken cancel);
        Task<List<PostReactionDTO>> GetPostReactionsAsync(long postId, CancellationToken cancel);

        // Comment operations
        Task<CommentDTO> AddCommentAsync(long postId, string userId, string content, string? parentCommentId, CancellationToken cancel);
        Task<List<CommentDTO>> GetCommentsAsync(long postId, CancellationToken cancel);
        Task UpdateCommentAsync(string commentId, string userId, string content, CancellationToken cancel);
        Task DeleteCommentAsync(string commentId, string userId, CancellationToken cancel);
    }

    public class PostFeedServiceImpl : IPostFeedService
    {
        private readonly AppDbContext _dbContext;
        private readonly IRealtimeService _realtimeService;
        private readonly INotificationService _notificationService;

        public PostFeedServiceImpl(AppDbContext dbContext, IRealtimeService realtimeService, INotificationService notificationService)
        {
            _dbContext = dbContext;
            _realtimeService = realtimeService;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Create a new post with optional media
        /// </summary>
        public async Task<PostResponseDTO> CreatePostAsync(string userId, PostMediaRequest postRequest, CancellationToken cancel)
        {
            if (postRequest == null)
                throw new AppException(StatusCodes.Status400BadRequest, "Post request cannot be null");

            if (string.IsNullOrWhiteSpace(userId))
                throw new AppException(StatusCodes.Status400BadRequest, "User ID cannot be empty");

            if (string.IsNullOrWhiteSpace(postRequest.Content))
                throw new AppException(StatusCodes.Status400BadRequest, "Post content cannot be empty");

            cancel.ThrowIfCancellationRequested();

            // Check if user exists
            var userExists = await _dbContext.Users.AnyAsync(u => u.Id == userId, cancel);
            if (!userExists)
                throw new AppException(StatusCodes.Status404NotFound, "User not found");

            // Create post
            var newPost = new Post
            {
                CreatedByUserId = userId,
                Content = postRequest.Content,
                Privacy = postRequest.Privacy ?? "Public",
                GroupId = postRequest.GroupId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await _dbContext.Posts.AddAsync(newPost, cancel);
            await _dbContext.SaveChangesAsync(cancel);

            // Add media if provided
            if (!string.IsNullOrEmpty(postRequest.MediaUrl))
            {
                var postMedia = new PostMedia
                {
                    Id = Guid.NewGuid().ToString(),
                    PostId = newPost.Id,
                    MediaType = postRequest.MediaType ?? "Image",
                    MediaUrl = postRequest.MediaUrl,
                    ThumbnailUrl = postRequest.ThumbnailUrl,
                    Height = postRequest.height ?? 700,
                    Width = postRequest.width ?? 400
                };

                await _dbContext.PostMedia.AddAsync(postMedia, cancel);
                await _dbContext.SaveChangesAsync(cancel);
            }

            // Create user feed entry
            var userFeed = new UserFeed
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                PostId = newPost.Id,
                SourceUserId = userId,
                SourceType = "Post",
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.UserFeeds.AddAsync(userFeed, cancel);
            await _dbContext.SaveChangesAsync(cancel);

            // Notify friends
            try
            {
                var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancel);
                await _realtimeService.PublishAsync("feed:updates", new
                {
                    type = "NEW_POST",
                    postId = newPost.Id,
                    userId = userId,
                    userName = user?.DisplayName ?? user?.Name,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Real-time notification failed: {ex.Message}");
            }

            return await GetPostAsync(newPost.Id, userId, cancel);
        }

        /// <summary>
        /// Get a single post with details
        /// </summary>
        public async Task<PostResponseDTO> GetPostAsync(long postId, string? currentUserId, CancellationToken cancel)
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
                throw new AppException(StatusCodes.Status404NotFound, "Post not found");

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
                .CountAsync(c => c.PostId == postId && string.IsNullOrEmpty(c.ParentCommentId), cancel);

            return new PostResponseDTO
            {
                Id = post.Post.Id,
                CreatedByUserId = post.Post.CreatedByUserId,
                CreatedByName = post.User.DisplayName ?? post.User.Name,
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
        public async Task<List<UserFeedDTO>> GetUserFeedAsync(string userId, SearchRequest request, CancellationToken cancel = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new AppException(StatusCodes.Status400BadRequest, "User ID cannot be empty");

          

            cancel.ThrowIfCancellationRequested();

            var skip = (request.Page - 1) * request.PageSize;

            var feeds = await _dbContext.UserFeeds
                .AsNoTracking()
                .Where(f => f.UserId == userId)
                .OrderByDescending(f => f.CreatedAt)
                .Skip(skip)
                .Take(request.PageSize)
                .Join(
                    _dbContext.Users,
                    f => f.SourceUserId,
                    u => u.Id,
                    (f, u) => new { Feed = f, SourceUser = u }
                )
                .ToListAsync(cancel);

            var result = new List<UserFeedDTO>();

            foreach (var item in feeds)
            {
                var post = await GetPostAsync(item.Feed.PostId, userId, cancel);

                result.Add(new UserFeedDTO
                {
                    Id = item.Feed.Id,
                    UserId = item.Feed.UserId,
                    PostId = item.Feed.PostId,
                    SourceUserId = item.Feed.SourceUserId,
                    SourceUserName = item.SourceUser.DisplayName ?? item.SourceUser.Name,
                    SourceType = item.Feed.SourceType,
                    CreatedAt = item.Feed.CreatedAt,
                    Post = post
                });
            }

            return result;
        }

        /// <summary>
        /// Get all posts from a user
        /// </summary>
        public async Task<List<PostResponseDTO>> GetUserPostsAsync(string userId, SearchRequest request, CancellationToken cancel = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new AppException(StatusCodes.Status400BadRequest, "User ID cannot be empty");

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
        public async Task UpdatePostAsync(long postId, string userId, string content, string privacy, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new AppException(StatusCodes.Status400BadRequest, "Content cannot be empty");

            cancel.ThrowIfCancellationRequested();

            var post = await _dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId, cancel);

            if (post == null)
                throw new AppException(StatusCodes.Status404NotFound, "Post not found");

            if (post.CreatedByUserId != userId)
                throw new AppException(StatusCodes.Status403Forbidden, "Not authorized to update this post");

            post.Content = content;
            post.Privacy = privacy ?? "Public";
            post.UpdatedAt = DateTime.UtcNow;

            _dbContext.Posts.Update(post);
            await _dbContext.SaveChangesAsync(cancel);
        }

        /// <summary>
        /// Delete a post (soft delete)
        /// </summary>
        public async Task DeletePostAsync(long postId, string userId, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            var post = await _dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId, cancel);

            if (post == null)
                throw new AppException(StatusCodes.Status404NotFound, "Post not found");

            if (post.CreatedByUserId != userId)
                throw new AppException(StatusCodes.Status403Forbidden, "Not authorized to delete this post");

            post.IsDeleted = true;
            post.UpdatedAt = DateTime.UtcNow;

            _dbContext.Posts.Update(post);
            await _dbContext.SaveChangesAsync(cancel);
        }

        /// <summary>
        /// Add a reaction to a post
        /// </summary>
        public async Task<PostReactionDTO> AddReactionAsync(long postId, string userId, byte reactionType, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            var post = await _dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId, cancel);
            if (post == null)
                throw new AppException(StatusCodes.Status404NotFound, "Post not found");

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
                        userName = user?.DisplayName ?? user?.Name,
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
        public async Task RemoveReactionAsync(long postId, string userId, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

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
        public async Task<List<PostReactionDTO>> GetPostReactionsAsync(long postId, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

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
        public async Task<CommentDTO> AddCommentAsync(long postId, string userId, string content, string? parentCommentId, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new AppException(StatusCodes.Status400BadRequest, "Comment content cannot be empty");

            cancel.ThrowIfCancellationRequested();

            var post = await _dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId, cancel);
            if (post == null)
                throw new AppException(StatusCodes.Status404NotFound, "Post not found");

            // Validate parent comment if provided
            if (!string.IsNullOrEmpty(parentCommentId))
            {
                var parentComment = await _dbContext.Comments
                    .FirstOrDefaultAsync(c => c.Id == parentCommentId && c.PostId == postId, cancel);

                if (parentComment == null)
                    throw new AppException(StatusCodes.Status404NotFound, "Parent comment not found");
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
                        userName = user?.DisplayName ?? user?.Name,
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
                UserName = user_info?.DisplayName ?? user_info?.Name ?? "Anonymous",
                UserAvatar = user_info?.AvatarUrl,
                Content = comment.Content,
                ParentCommentId = comment.ParentCommentId,
                CreatedAt = comment.CreatedAt
            };
        }

        /// <summary>
        /// Get comments on a post with replies
        /// </summary>
        public async Task<List<CommentDTO>> GetCommentsAsync(long postId, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            var comments = await _dbContext.Comments
                .AsNoTracking()
                .Where(c => c.PostId == postId && string.IsNullOrEmpty(c.ParentCommentId))
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
                    .Where(c => c.ParentCommentId == item.Comment.Id)
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
                        UserName = x.User.DisplayName ?? x.User.Name,
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
                    UserName = item.User.DisplayName ?? item.User.Name,
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
        public async Task UpdateCommentAsync(string commentId, string userId, string content, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new AppException(StatusCodes.Status400BadRequest, "Comment content cannot be empty");

            cancel.ThrowIfCancellationRequested();

            var comment = await _dbContext.Comments.FirstOrDefaultAsync(c => c.Id == commentId, cancel);

            if (comment == null)
                throw new AppException(StatusCodes.Status404NotFound, "Comment not found");

            if (comment.UserId != userId)
                throw new AppException(StatusCodes.Status403Forbidden, "Not authorized to update this comment");

            comment.Content = content;
            comment.UpdatedAt = DateTime.UtcNow;

            _dbContext.Comments.Update(comment);
            await _dbContext.SaveChangesAsync(cancel);
        }

        /// <summary>
        /// Delete a comment (soft delete)
        /// </summary>
        public async Task DeleteCommentAsync(string commentId, string userId, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            var comment = await _dbContext.Comments.FirstOrDefaultAsync(c => c.Id == commentId, cancel);

            if (comment == null)
                throw new AppException(StatusCodes.Status404NotFound, "Comment not found");

            if (comment.UserId != userId)
                throw new AppException(StatusCodes.Status403Forbidden, "Not authorized to delete this comment");

            comment.DeletedAt = DateTime.UtcNow;

            _dbContext.Comments.Update(comment);
            await _dbContext.SaveChangesAsync(cancel);
        }

        // Deprecated - use CreatePostAsync instead
        public async Task PostFeed(PostMediaRequest postRequest, CancellationToken cancel)
        {
            if (postRequest == null)
                throw new AppException(StatusCodes.Status400BadRequest, "Post request cannot be null");

            if (string.IsNullOrWhiteSpace(postRequest.CreatedByUserId))
                throw new AppException(StatusCodes.Status400BadRequest, "User ID cannot be empty");

            await CreatePostAsync(postRequest.CreatedByUserId, postRequest, cancel);
        }
    }
}