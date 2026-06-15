using Hangfire;
using Kpett.ChatApp.Constants;
using Kpett.ChatApp.DTOs.Payload.Cursor;
using Kpett.ChatApp.DTOs.Request.Post;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Media;
using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.DTOs.Response.User;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Extensions;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Kpett.ChatApp.Services.Impls
{
    public class PostService : IPostService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<PostService> _logger;

        private readonly string avatarType = UserMediaType.Avatar.GetDescription();
        public PostService(AppDbContext dbContext, ILogger<PostService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Create a new post
        /// </summary>
        public async Task<PostFeedResponse> CreatePostAsync(string userId, PostRequest postRequest, CancellationToken cancel)
        {
            _logger.LogInformation("User {UserId} creating a new post", userId);

            if (string.IsNullOrWhiteSpace(userId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "User ID cannot be empty");

            var avatarType = UserMediaType.Avatar.GetDescription();
            var user = await _dbContext.Users
                .Where(u => u.Id == userId)
                .Select(u => new UserResponse
                {
                    Id = u.Id,
                    Email = u.Email,
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                    IsVerified = u.IsVerified,
                    AvatarUrl = _dbContext.UserMedias
                                    .Where(um => um.UserId == userId && um.IsPrimary && um.MediaType == avatarType)
                                    .Select(um => um.MediaUrl)
                                    .FirstOrDefault()
                })
                .FirstOrDefaultAsync(cancel);

            if (user == null)
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");

            var newPost = new Post
            {
                Id = Guid.NewGuid().ToString(),
                CreatedByUserId = userId,
                Content = postRequest.Content,
                Privacy = postRequest.Privacy ?? PostPrivacy.Public.GetDescription(),
                Type = PostType.Post.GetDescription(),
                GroupId = postRequest.GroupId,
                CreatedAt = DateTime.UtcNow,
                PinnedAt = DateTime.UtcNow,
                IsDeleted = false,
                LikeCount = 0,
                CommentCount = 0
            };

            await _dbContext.Posts.AddAsync(newPost, cancel);

            await SyncPostMediaAsync(newPost.Id, postRequest.Media, cancel);

            await _dbContext.SaveChangesAsync(cancel);

            var mediaResponse = postRequest.Media?.Select(m => new MediaPostResponse
            {
                PublicId = m.PublicId,
                Url = m.Url,
                Type = m.Type
            }).ToList() ?? new List<MediaPostResponse>();

            if (postRequest.Media != null && postRequest.Media.Any())
            {
                var publicIdsToConfirm = postRequest.Media.Select(m => m.PublicId).ToList();

                BackgroundJob.Enqueue<IMediaService>(x => x.ConfirmMediaOnCloudinaryAsync(publicIdsToConfirm));
            }

            _logger.LogInformation("User {UserId} created a new post with ID {PostId}", userId, newPost.Id);

            return BuildPostResponse(newPost, user, mediaResponse, isLiked: false);
        }

        /// <summary>
        /// Update a post
        /// </summary>
        public async Task<PostFeedResponse> UpdatePostAsync(string postId, string userId, PostRequest postRequest, CancellationToken cancel)
        {
            _logger.LogInformation("User {UserId} updating post with ID {PostId}", userId, postId);

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "User ID cannot be empty");
            }

            var post = await _dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId, cancel);

            if (post == null)
            {
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");
            }

            var user = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new UserResponse
                {
                    Id = u.Id,
                    Email = u.Email,
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                    IsVerified = u.IsVerified,
                })
                .FirstOrDefaultAsync(cancel);

            if (user == null)
            {
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");
            }

            if (post.CreatedByUserId != userId)
            {
                throw new ForbiddenException(ErrorCodes.POST.USER_NOT_AUTHORIZED, "Not authorized to update this post");
            }

            post.Content = postRequest.Content;
            post.Privacy = postRequest.Privacy ?? post.Privacy;
            post.UpdatedAt = DateTime.UtcNow;

            await SyncPostMediaAsync(post.Id, postRequest.Media, cancel);

            await _dbContext.SaveChangesAsync(cancel);

            if (postRequest.Media != null && postRequest.Media.Any())
            {
                var publicIdsToConfirm = postRequest.Media.Select(m => m.PublicId).ToList();

                BackgroundJob.Enqueue<IMediaService>(x => x.ConfirmMediaOnCloudinaryAsync(publicIdsToConfirm));
            }

            var allCurrentMedias = await _dbContext.PostMedia
                .Where(m => m.PostId == post.Id && !m.IsTemporary)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new MediaPostResponse
                {
                    PublicId = m.Id,
                    Url = m.MediaUrl,
                    Type = m.MediaType
                })
                .ToListAsync(cancel);

            var authorResponse = new UserResponse
            {
                Id = user.Id,
                Email = user.Email,
                Username = user.Username,
                DisplayName = user.DisplayName,
                IsVerified = user.IsVerified,
                AvatarUrl = user.AvatarUrl,
            };

            bool isLiked = await _dbContext.PostReactions.AnyAsync(pr => pr.PostId == postId && pr.UserId == userId, cancel);

            _logger.LogInformation("User {UserId} updated post with ID {PostId}", userId, postId);

            return BuildPostResponse(post, authorResponse, allCurrentMedias, isLiked);
        }

        /// <summary>
        /// Get a single post with details
        /// </summary>
        public async Task<PostFeedResponse> GetPostByIdAsync(string postId, string? currentUserId, CancellationToken cancel)
        {
            var post = await _dbContext.Posts
                .AsNoTracking()
                .Where(p => p.Id == postId && p.IsDeleted == false)
                .Select(p => new PostFeedResponse
                {
                    Id = p.Id,
                    Content = p.Content,
                    Privacy = p.Privacy,
                    Type = p.Type,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,

                    Author = _dbContext.Users.Where(u => u.Id == p.CreatedByUserId)
                                             .Select(u => new UserResponse
                                             {
                                                 Id = u.Id,
                                                 Email = u.Email,
                                                 Username = u.Username,
                                                 DisplayName = u.DisplayName,
                                                 AvatarUrl = _dbContext.UserMedias
                                                            .Where(um => um.UserId == u.Id && um.MediaType == avatarType && um.IsPrimary)
                                                            .Select(um => um.MediaUrl)
                                                            .FirstOrDefault(),
                                                 IsVerified = u.IsVerified,
                                             })
                                             .FirstOrDefault(),

                    Media = _dbContext.PostMedia.Where(m => m.PostId == p.Id)
                                         .OrderBy(m => m.CreatedAt)
                                         .Select(m => new MediaPostResponse { PublicId = m.Id, Url = m.MediaUrl, Type = m.MediaType })
                                         .ToList(),

                    Metrics = new PostMetricsResponse
                    {
                        LikeCount = p.LikeCount,
                        CommentCount = p.CommentCount
                    },

                    ViewerContext = new PostViewerContextResponse
                    {
                        IsOwner = p.CreatedByUserId == currentUserId,
                        IsLiked = _dbContext.PostReactions.Any(r => r.PostId == p.Id && r.UserId == currentUserId),
                        IsSaved = false,
                        IsPinned = false,
                        CanEdit = p.CreatedByUserId == currentUserId,
                        CanDelete = p.CreatedByUserId == currentUserId,
                        CanLike = true,
                        CanComment = true,
                        CanPin = p.CreatedByUserId == currentUserId
                    },
                })
                .AsSplitQuery()
                .FirstOrDefaultAsync(cancel);

            if (post == null)
            {
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");
            }

            _logger.LogInformation("User {UserId} retrieved post with ID {PostId}", currentUserId ?? "Anonymous", postId);

            post.CreatedAt = post.CreatedAt.ToUtc();
            return post;
        }

        /// <summary>
        /// Get user feed with pagination
        /// </summary>
        public async Task<PaginatedData<PostFeedResponse>> GetFeedAsync(string? currentUserId, string? cursor = null, int limit = 10, CancellationToken cancel = default)
        {
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

            var query = _dbContext.Posts
                .AsNoTracking()
                .Where(p => !p.IsDeleted);

            if (cursorDate.HasValue && !string.IsNullOrEmpty(cursorId))
            {
                query = query.Where(p =>
                    p.CreatedAt < cursorDate.Value ||
                    (p.CreatedAt == cursorDate.Value && p.Id.CompareTo(cursorId) < 0));
            }

            var fetchedPosts = await query
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Take(limit + 1)
                .Select(p => new PostFeedResponse
                {
                    Id = p.Id,
                    Content = p.Content,
                    Privacy = p.Privacy,
                    Type = p.Type,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,

                    Author = _dbContext.Users.Where(u => u.Id == p.CreatedByUserId)
                                             .Select(u => new UserResponse
                                             {
                                                 Id = u.Id,
                                                 Email = u.Email,
                                                 Username = u.Username,
                                                 DisplayName = u.DisplayName,
                                                 AvatarUrl = _dbContext.UserMedias
                                                            .Where(um => um.UserId == u.Id && um.MediaType == avatarType && um.IsPrimary)
                                                            .Select(um => um.MediaUrl)
                                                            .FirstOrDefault(),
                                                 IsVerified = u.IsVerified,
                                             })
                                             .FirstOrDefault(),

                    Media = _dbContext.PostMedia.Where(m => m.PostId == p.Id)
                                         .OrderBy(m => m.CreatedAt)
                                         .Select(m => new MediaPostResponse { PublicId = m.Id, Url = m.MediaUrl, Type = m.MediaType })
                                         .ToList(),

                    Metrics = new PostMetricsResponse
                    {
                        LikeCount = p.LikeCount,
                        CommentCount = p.CommentCount
                    },

                    ViewerContext = new PostViewerContextResponse
                    {
                        IsOwner = p.CreatedByUserId == currentUserId,
                        IsLiked = _dbContext.PostReactions.Any(r => r.PostId == p.Id && r.UserId == currentUserId),
                        IsSaved = false,
                        IsPinned = false,
                        CanEdit = p.CreatedByUserId == currentUserId,
                        CanDelete = p.CreatedByUserId == currentUserId,
                        CanLike = true,
                        CanComment = true,
                        CanPin = p.CreatedByUserId == currentUserId
                    },
                })
                .AsSplitQuery()
                .ToListAsync(cancel);

            if (!fetchedPosts.Any())
            {
                return new PaginatedData<PostFeedResponse>
                {
                    Items = new List<PostFeedResponse>(),
                    Pagination = new CursorPaginationMeta { Limit = limit }
                };
            }

            string? nextCursor = null;

            if (fetchedPosts.Count > limit)
            {
                var lastItemInPage = fetchedPosts[limit - 1];
                nextCursor = CursorHelper.Encode(new BaseCursorPayload
                {
                    Id = lastItemInPage.Id,
                    CreatedAt = lastItemInPage.CreatedAt
                });
                fetchedPosts = fetchedPosts.Take(limit).ToList();
            }

            foreach (var post in fetchedPosts)
            {
                post.CreatedAt = post.CreatedAt.ToUtc();
                post.UpdatedAt = post.UpdatedAt?.ToUtc();
            }

            _logger.LogInformation("User {UserId} retrieved feed with {Count} posts", currentUserId ?? "Anonymous", fetchedPosts.Count);

            return new PaginatedData<PostFeedResponse>
            {
                Items = fetchedPosts,
                Pagination = new CursorPaginationMeta
                {
                    NextCursor = nextCursor,
                    Limit = limit
                }
            };
        }

        /// <summary>
        /// Get posts created by a specific user with pagination and optional type filter
        /// </summary>
        public async Task<PaginatedData<PostThumbnailResponse>> GetPostsByUserIdAsync(string userId, string? currentUserId, SearchRequest searchRequest, CursorPaginationRequest cursorPagination, CancellationToken cancel = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "User ID cannot be empty");
            }

            var author = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new UserResponse
                {
                    Id = u.Id,
                    Email = u.Email,
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                    AvatarUrl = _dbContext.UserMedias
                        .Where(um => um.UserId == u.Id && um.MediaType == avatarType && um.IsPrimary)
                        .Select(um => um.MediaUrl)
                        .FirstOrDefault(),
                    IsVerified = u.IsVerified,
                })
                .FirstOrDefaultAsync(cancel);

            if (author == null)
            {
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");
            }

            var normalizedType = string.IsNullOrWhiteSpace(searchRequest?.Type)
                ? PostType.Post.GetDescription()
                : searchRequest.Type;
            var normalizedLimit = cursorPagination.Limit <= 0
                ? 10
                : Math.Min(cursorPagination.Limit, 50);

            DateTime? cursorDate = null;
            DateTime? cursorPinnedAt = null;
            string? cursorId = null;
            if (!string.IsNullOrWhiteSpace(cursorPagination.Cursor))
            {
                var decoded = CursorHelper.Decode<PostThumbnailCursorPayload>(cursorPagination.Cursor);
                if (decoded != null)
                {
                    cursorDate = decoded.CreatedAt;
                    cursorPinnedAt = decoded.PinnedAt;
                    cursorId = decoded.Id;
                }
            }

            var query = _dbContext.Posts
                .AsNoTracking()
                .Where(p => p.CreatedByUserId == userId && !p.IsDeleted)
                .Where(p => p.Type == normalizedType);

            if (cursorDate.HasValue && !string.IsNullOrEmpty(cursorId))
            {
                query = query
                    .Where(p =>
                        p.PinnedAt < cursorPinnedAt ||

                        (p.PinnedAt == cursorPinnedAt && p.CreatedAt < cursorDate.Value) ||

                        (p.PinnedAt == cursorPinnedAt && p.CreatedAt == cursorDate.Value && string.Compare(p.Id, cursorId) < 0)
                    );
            }

            var rawData = await query
                .OrderByDescending(p => p.PinnedAt)
                .ThenByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Take(normalizedLimit + 1)
                .Select(p => new
                {
                    p.Id,
                    p.Content,
                    p.CreatedAt,
                    p.UpdatedAt,
                    p.Privacy,
                    p.Type,
                    p.CreatedByUserId,
                    p.PinnedAt
                })
                .ToListAsync(cancel);

            if (!rawData.Any())
            {
                return new PaginatedData<PostThumbnailResponse>
                {
                    Items = new List<PostThumbnailResponse>(),
                    Pagination = new CursorPaginationMeta { Limit = normalizedLimit }
                };
            }

            string? nextCursor = null;
            var itemsToProcess = rawData;

            if (rawData.Count > normalizedLimit)
            {
                var lastItemInPage = rawData[normalizedLimit - 1];
                nextCursor = CursorHelper.Encode(new PostThumbnailCursorPayload
                {
                    Id = lastItemInPage.Id,
                    CreatedAt = lastItemInPage.CreatedAt,
                    PinnedAt = lastItemInPage.PinnedAt
                });
                itemsToProcess = rawData.Take(normalizedLimit).ToList();
            }

            var postIds = itemsToProcess.Select(data => data.Id).ToList();

            var mediaRows = await _dbContext.PostMedia
                .AsNoTracking()
                .Where(m => m.PostId != null && postIds.Contains(m.PostId))
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    PostId = m.PostId!,
                    m.Id,
                    m.MediaUrl,
                    m.MediaType,
                    m.CreatedAt
                })
                .ToListAsync(cancel);

            var mediaByPostId = mediaRows
                .GroupBy(m => m.PostId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            var likeCounts = await _dbContext.PostReactions
                .AsNoTracking()
                .Where(r => postIds.Contains(r.PostId))
                .GroupBy(r => r.PostId)
                .Select(group => new
                {
                    PostId = group.Key,
                    Count = group.Count()
                })
                .ToDictionaryAsync(x => x.PostId, x => x.Count, cancel);

            var commentCounts = await _dbContext.Comments
                .AsNoTracking()
                .Where(c => postIds.Contains(c.PostId) && c.DeletedAt == null)
                .GroupBy(c => c.PostId)
                .Select(group => new
                {
                    PostId = group.Key,
                    Count = group.Count()
                })
                .ToDictionaryAsync(x => x.PostId, x => x.Count, cancel);

            var likedPostIds = string.IsNullOrWhiteSpace(currentUserId)
                ? new HashSet<string>(StringComparer.Ordinal)
                : (await _dbContext.PostReactions
                    .AsNoTracking()
                    .Where(r => r.UserId == currentUserId && postIds.Contains(r.PostId))
                    .Select(r => r.PostId)
                    .Distinct()
                    .ToListAsync(cancel))
                .ToHashSet(StringComparer.Ordinal);

            var mappedPosts = itemsToProcess.Select(data => new PostThumbnailResponse
            {
                Id = data.Id,
                Content = data.Content,
                CreatedAt = data.CreatedAt,
                UpdatedAt = data.UpdatedAt,
                Privacy = data.Privacy,
                Type = data.Type,

                Author = new UserResponse
                {
                    Id = author.Id,
                    Username = author.Username,
                    AvatarUrl = author.AvatarUrl,
                    DisplayName = author.DisplayName,
                    Email = author.Email,
                    IsVerified = author.IsVerified,
                },

                MediaThumbnail = mediaByPostId.TryGetValue(data.Id, out var media) ? new MediaPostResponse
                {
                    PublicId = media.Id,
                    Url = media.MediaUrl,
                    Type = media.MediaType
                } : null,

                Metrics = new PostMetricsResponse
                {
                    LikeCount = likeCounts.GetValueOrDefault(data.Id, 0),
                    CommentCount = commentCounts.GetValueOrDefault(data.Id, 0)
                },

                ViewerContext = new PostViewerContextResponse
                {
                    IsOwner = data.CreatedByUserId == currentUserId,
                    IsLiked = likedPostIds.Contains(data.Id),
                    IsSaved = false,
                    IsPinned = false,
                    CanEdit = data.CreatedByUserId == currentUserId,
                    CanDelete = data.CreatedByUserId == currentUserId,
                    CanLike = true,
                    CanComment = true,
                    CanPin = data.CreatedByUserId == currentUserId
                },
            }).ToList();

            _logger.LogInformation("User {UserId} retrieved posts for user {AuthorId} with {Count} posts", currentUserId ?? "Anonymous", userId, mappedPosts.Count);

            return new PaginatedData<PostThumbnailResponse>
            {
                Items = mappedPosts,
                Pagination = new CursorPaginationMeta
                {
                    NextCursor = nextCursor,
                    Limit = normalizedLimit
                }
            };
        }

        /// <summary>
        /// Delete a post (soft delete)
        /// </summary>
        public async Task DeletePostAsync(string postId, string userId, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            var post = await _dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId, cancel);

            if (post == null)
            {
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");
            }

            if (post.CreatedByUserId != userId)
            {
                throw new ForbiddenException(ErrorCodes.POST.USER_NOT_AUTHORIZED, "Not authorized to delete this post");
            }

            post.IsDeleted = true;
            post.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation("User {UserId} deleted post with ID {PostId}", userId, postId);

            _dbContext.Posts.Update(post);
            await _dbContext.SaveChangesAsync(cancel);
        }

        /// <summary>
        /// Add a reaction to a post
        /// </summary>
        public async Task<PostReactionDTO> AddReactionAsync(string postId, string userId, byte reactionType, CancellationToken cancel)
        {
            if (reactionType == 0)
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Reaction type is required");
            }

            var post = await _dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId);
            if (post == null)
            {
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");
            }

            _logger.LogInformation("User {UserId} added reaction to post with ID {PostId}", userId, postId);

            // Check if already reacted
            var existingReaction = await _dbContext.PostReactions
                .FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == userId);

            var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
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

        /// <summary>
        /// Remove a reaction from a post
        /// </summary>
        public async Task RemoveReactionAsync(string postId, string userId, CancellationToken cancel)
        {
            _logger.LogInformation("User {UserId} is removing reaction from post with ID {PostId}", userId, postId);

            var postExists = await _dbContext.Posts
                .AnyAsync(p => p.Id == postId && !p.IsDeleted);
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

        /// <summary>
        /// Get all reactions on a post
        /// </summary>
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

        // HELPER METHODS

        // Helper method to sync media with a post during creation or update
        private async Task SyncPostMediaAsync(string postId, IEnumerable<MediaRequest>? requestedMedia, CancellationToken cancel)
        {
            // Láº¥y danh sÃ¡ch Media hiá»‡n táº¡i tá»« DB
            var currentMedias = await _dbContext.PostMedia
                .Where(m => m.PostId == postId)
                .ToListAsync(cancel);

            var requestedMediaList = requestedMedia?.ToList() ?? new List<MediaRequest>();
            var requestedMediaIds = requestedMediaList.Select(m => m.PublicId).ToList();

            // Tìm và xóa các media đã bị xóa trên client
            var mediasToRemove = currentMedias
                .Where(m => !requestedMediaIds.Contains(m.Id))
                .ToList();

            if (mediasToRemove.Any())
            {
                _dbContext.PostMedia.RemoveRange(mediasToRemove);
            }

            if (!requestedMediaList.Any())
            {
                return;
            }

            foreach (var reqMedia in requestedMediaList)
            {
                var existingMedia = currentMedias.FirstOrDefault(m => m.Id == reqMedia.PublicId);

                if (existingMedia == null)
                {
                    var newMedia = new PostMedia
                    {
                        Id = reqMedia.PublicId,
                        PostId = postId,
                        MediaUrl = reqMedia.Url,
                        MediaType = reqMedia.Type,
                        IsTemporary = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _dbContext.PostMedia.AddAsync(newMedia, cancel);
                }
            }
        }

        /// Hàm gọi lên Cloudinary để gỡ bỏ tag 'status_temp' và thêm tag 'active'
        // Helper method to build PostFeedResponse from Post entity
        private PostFeedResponse BuildPostResponse(Post post, UserResponse author, List<MediaPostResponse> mediaResponse, bool isLiked)
        {
            return new PostFeedResponse
            {
                Id = post.Id,
                Author = author,
                Content = post.Content,
                Media = mediaResponse,
                Privacy = post.Privacy,
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt,

                Metrics = new PostMetricsResponse
                {
                    LikeCount = post.LikeCount,
                    CommentCount = post.CommentCount
                },

                ViewerContext = new PostViewerContextResponse
                {
                    IsOwner = true,
                    IsLiked = isLiked,
                    IsSaved = false,
                    IsPinned = post.IsPinned,
                    CanEdit = true,
                    CanDelete = true,
                    CanPin = true,
                    CanLike = true,
                    CanComment = true
                }
            };
        }
    }
}


