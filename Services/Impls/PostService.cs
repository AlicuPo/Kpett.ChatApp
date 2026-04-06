using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Payload.Cursor;
using Kpett.ChatApp.DTOs.Request.Post;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Media;
using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.DTOs.Response.User;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Extentions;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Receive;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;

namespace Kpett.ChatApp.Services.Impls
{
    public class PostService : IPostService
    {
        private readonly AppDbContext _dbContext;
        private readonly IRealtimeService _realtimeService;
        private readonly INotificationService _notificationService;
        private readonly IMediaService _mediaService;
        public PostService(AppDbContext dbContext, IRealtimeService realtimeService, INotificationService notificationService, IMediaService mediaService)
        {
            _dbContext = dbContext;
            _realtimeService = realtimeService;
            _notificationService = notificationService;
            _mediaService = mediaService;
        }

        /// <summary>
        /// Create a new post
        /// </summary>
        public async Task<PostFeedResponse> CreatePostAsync(string userId, PostRequest postRequest, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "User ID cannot be empty");

            cancel.ThrowIfCancellationRequested();

            // Check if user exists
            var user = await _dbContext.Users
                              .Where(u => u.Id == userId)
                              .Select(u => new UserResponse
                              {
                                  Id = u.Id,
                                  Email = u.Email,
                                  Username = u.Username,
                                  DisplayName = u.DisplayName,
                                  IsVerified = u.IsVerified,
                                  AvatarUrl = u.AvatarUrl,
                              })
                              .FirstOrDefaultAsync(cancel);
            if (user == null)
            {
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");
            }

            // Create post
            var newPost = new Post
            {
                Id = Guid.NewGuid().ToString(),
                CreatedByUserId = userId,
                Content = postRequest.Content,
                Privacy = postRequest.Privacy ?? PostPrivacy.Public.GetDescription(),
                Type = PostType.Post.GetDescription(),
                GroupId = postRequest.GroupId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await _dbContext.Posts.AddAsync(newPost, cancel);

            var media = postRequest.Media;

            if (media?.Any() == true)
            {
                var mediaIds = media.Select(m => m.PublicId).ToList();

                var existingMedias = await _dbContext.PostMedia
                    .Where(m => mediaIds.Contains(m.Id))
                    .ToDictionaryAsync(m => m.Id, cancel);

                foreach (var m in media)
                {
                    if (existingMedias.TryGetValue(m.PublicId, out var existingMedia))
                    {
                        existingMedia.PostId = newPost.Id;
                        existingMedia.MediaUrl = m.Url;
                        existingMedia.IsTemporary = false;
                        existingMedia.MediaType = m.Type;
                    }
                }
            }

            await _dbContext.SaveChangesAsync();

            // Map media response
            var mediaResponse = media?.Select(m => new MediaPostResponse
            {
                PublicId = m.PublicId,
                Url = m.Url,
                Type = m.Type
            }).ToList() ?? new List<MediaPostResponse>();

            // Initialize metrics and viewer context
            var metrics = new PostMetricsResponse
            {
                LikeCount = 0,
                CommentCount = 0
            };

            // Since the creator is viewing their own post immediately after creation, we can set the viewer context accordingly
            var viewerContext = new PostViewerContextResponse
            {
                IsOwner = true,
                IsLiked = false,
                IsSaved = false,
                IsPinned = false,
                CanEdit = true,
                CanDelete = true,
                CanLike = true,
                CanComment = true,
                CanPin = true
            };

            // Notify followers about the new post
            return new PostFeedResponse
            {
                Id = newPost.Id,
                Author = user,
                Content = newPost.Content,
                Media = mediaResponse,
                Metrics = metrics,
                Privacy = newPost.Privacy,
                ViewerContext = viewerContext,

                CreatedAt = newPost.CreatedAt,
                UpdatedAt = newPost.UpdatedAt,
            };
        }

        /// <summary>
        /// Update a post
        /// </summary>
        public async Task<PostFeedResponse> UpdatePostAsync(string postId, string userId, PostRequest postRequest, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "User ID cannot be empty");
            }

            var post = await _dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId, cancel);
            if (post == null)
            {
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");
            }
            if (post.CreatedByUserId != userId)
            {
                throw new ForbiddenException(ErrorCodes.POST.USER_NOT_AUTHORIZED, "Not authorized to update this post");
            }

            var user = await _dbContext.Users
                .Where(u => u.Id == userId)
                .Select(u => new UserResponse
                {
                    Id = u.Id,
                    Email = u.Email,
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                    IsVerified = u.IsVerified,
                    AvatarUrl = u.AvatarUrl,
                })
                .FirstOrDefaultAsync(cancel);

            post.Content = postRequest.Content;
            post.Privacy = postRequest.Privacy ?? post.Privacy;
            post.UpdatedAt = DateTime.UtcNow;

            var media = postRequest.Media;

            if (media?.Any() == true)
            {
                var mediaIds = media.Select(m => m.PublicId).ToList();

                var existingMedias = await _dbContext.PostMedia
                    .Where(m => mediaIds.Contains(m.Id))
                    .ToDictionaryAsync(m => m.Id, cancel);

                foreach (var m in media)
                {
                    if (existingMedias.TryGetValue(m.PublicId, out var existingMedia))
                    {
                        existingMedia.PostId = post.Id;
                        existingMedia.MediaUrl = m.Url;
                        existingMedia.IsTemporary = false;
                        existingMedia.MediaType = m.Type;
                    }
                }
            }

            await _dbContext.SaveChangesAsync();

            // Map media response
            var mediaResponse = media?.Select(m => new MediaPostResponse
            {
                PublicId = m.PublicId,
                Url = m.Url,
                Type = m.Type
            }).ToList() ?? new List<MediaPostResponse>();

            // Initialize metrics and viewer context
            var metrics = new PostMetricsResponse
            {
                LikeCount = await _dbContext.PostReactions.CountAsync(pr => pr.PostId == postId, cancel),
                CommentCount = await _dbContext.Comments.CountAsync(pr => pr.PostId == postId && pr.DeletedAt == null, cancel)
            };

            // Since the creator is viewing their own post immediately after creation, we can set the viewer context accordingly
            var viewerContext = new PostViewerContextResponse
            {
                IsOwner = true,
                IsLiked = await _dbContext.PostReactions.AnyAsync(pr => pr.PostId == postId && pr.UserId == userId, cancel),
                IsSaved = false,
                IsPinned = post.IsPinned,
                CanEdit = true,
                CanDelete = true,
                CanLike = true,
                CanComment = true,
                CanPin = true
            };

            // Notify followers about the new post
            return new PostFeedResponse
            {
                Id = post.Id,
                Author = user,
                Content = post.Content,
                Media = mediaResponse,
                Metrics = metrics,
                Privacy = post.Privacy,
                ViewerContext = viewerContext,

                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt,
            };
        }


        /// <summary>
        /// Get a single post with details
        /// </summary>
        public async Task<PostFeedResponse> GetPostByIdAsync(string postId, string? currentUserId, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            var post = await _dbContext.Posts
                .AsNoTracking()
                .Where(p => p.Id == postId && p.IsDeleted == false)
                .Select(p => new
                {
                    Post = p,

                    User = _dbContext.Users.Where(u => u.Id == p.CreatedByUserId)
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

                    Media = _dbContext.PostMedia.Where(m => m.PostId == p.Id)
                                         .Select(m => new { m.Id, m.MediaUrl, m.MediaType, m.CreatedAt })
                                         .ToList(),

                    LikeCount = _dbContext.PostReactions.Count(r => r.PostId == p.Id),
                    CommentCount = _dbContext.Comments.Count(c => c.PostId == p.Id && c.DeletedAt == null),
                    IsLiked = _dbContext.PostReactions.Any(r => r.PostId == p.Id && r.UserId == currentUserId)
                })
                .FirstOrDefaultAsync(cancel);

            if (post == null)
            {
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");
            }

            return new PostFeedResponse
            {
                Id = post.Post.Id,
                Content = post.Post.Content,
                Privacy = post.Post.Privacy,
                Author = new UserResponse
                {
                    Id = post.User.Id,
                    Username = post.User.Username,
                    AvatarUrl = post.User.AvatarUrl,
                    DisplayName = post.User.DisplayName,
                    Email = post.User.Email,
                    IsVerified = post.User.IsVerified,
                },
                Media = post.Media
                .OrderBy(m => m.CreatedAt)
                .Select(m => new MediaPostResponse
                {
                    PublicId = m.Id,
                    Url = m.MediaUrl,
                    Type = m.MediaType
                }).ToList(),

                ViewerContext = new PostViewerContextResponse
                {
                    IsOwner = post.Post.CreatedByUserId == currentUserId,
                    IsLiked = post.IsLiked,
                    IsSaved = false,
                    IsPinned = false,
                    CanEdit = post.Post.CreatedByUserId == currentUserId,
                    CanDelete = post.Post.CreatedByUserId == currentUserId,
                    CanLike = true,
                    CanComment = true,
                    CanPin = post.Post.CreatedByUserId == currentUserId
                },

                Metrics = new PostMetricsResponse
                {
                    LikeCount = post.LikeCount,
                    CommentCount = post.CommentCount
                },

                CreatedAt = post.Post.CreatedAt,
                UpdatedAt = post.Post.UpdatedAt,
            };
        }

        /// <summary>
        /// Get user feed with pagination
        /// </summary>
        public async Task<PaginatedData<PostFeedResponse>> GetFeedAsync(string currentUserId, string? cursor = null, int limit = 10, CancellationToken cancel = default)
        {
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
                                         .Select(m => new { m.Id, m.MediaUrl, m.MediaType, CreatedAt = m.CreatedAt })
                                         .ToList(),

                    LikeCount = _dbContext.PostReactions.Count(r => r.PostId == p.Id),
                    CommentCount = _dbContext.Comments.Count(c => c.PostId == p.Id && c.DeletedAt == null),
                    IsLiked = _dbContext.PostReactions.Any(r => r.PostId == p.Id && r.UserId == currentUserId)
                })
                .ToListAsync(cancel);

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
                nextCursor = CursorHelper.Encode(new BaseCursorPayload
                {
                    Id = lastItemInPage.Id,
                    CreatedAt = lastItemInPage.CreatedAt
                });
                itemsToProcess = rawData.Take(limit).ToList();
            }

            var mappedPosts = itemsToProcess.Select(data => new PostFeedResponse
            {
                Id = data.Post.Id,
                Content = data.Post.Content,
                CreatedAt = data.Post.CreatedAt,
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

                Media = data.Medias
                .OrderBy(m => m.CreatedAt)
                .Select(m => new MediaPostResponse
                {
                    PublicId = m.Id,
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
                },
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

        /// <summary>
        /// Get all posts from a user
        /// </summary>
        public async Task<PaginatedData<PostThumbnailResponse>> GetPostsByUserIdAsync(string userId, string currentUserId, SearchRequest searchRequest, CursorPaginationRequest cursorPagination, CancellationToken cancel = default)
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
                    IsVerified = u.IsVerified,
                    AvatarUrl = u.AvatarUrl,
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

            return null;
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

        public async Task DeleteMedia(string publicId, [FromQuery] string resourceType)
        {
            // Tìm ảnh trong DB và kiểm tra quyền sở hữu
            var mediaRecord = await _dbContext.PostMedia.FirstOrDefaultAsync(m => m.Id == publicId);

            if (mediaRecord == null)
            {
                throw new NotFoundException(ErrorCodes.MEDIA.NOT_FOUND, "File not found");
            }

            // Gọi Cloudinary SDK để xóa file thực tế trên mây
            bool isDeletedFromCloud = await _mediaService.DeleteFileAsync(publicId, resourceType);

            if (!isDeletedFromCloud)
            {
                throw new Exception("Không thể xóa file trên Cloudinary.");
            }

            // Xóa record trong Database
            _dbContext.PostMedia.Remove(mediaRecord);
            await _dbContext.SaveChangesAsync();
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
                    Id = Guid.NewGuid().ToString(),
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
                .AnyAsync(p => p.Id == postId && !p.IsDeleted, cancel);
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

        public async Task<CommentDTO> AddCommentAsync(
            string postId,
            string userId,
            string content,
            string? parentCommentId,
            CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Comment content cannot be empty");

            cancel.ThrowIfCancellationRequested();

            var utcNow = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
            var normalizedParentCommentId = NormalizeOptionalString(parentCommentId);
            var post = await _dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted, cancel);
            if (post == null)
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");

            Comment? parentComment = null;
            if (!string.IsNullOrEmpty(normalizedParentCommentId))
            {
                parentComment = await _dbContext.Comments
                    .FirstOrDefaultAsync(c => c.Id == normalizedParentCommentId && c.PostId == postId && c.DeletedAt == null, cancel);

                if (parentComment == null)
                    throw new NotFoundException(ErrorCodes.COMMENT.PARENT_COMMENT_NOT_FOUND, "Parent comment not found");
            }

            var user = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .FirstOrDefaultAsync(cancel);

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

            await SyncCommentMentionsAsync(comment.Id, ExtractMentionUserIds(content), utcNow, cancel);
            await _dbContext.SaveChangesAsync(cancel);

            return await MapCommentAsync(comment, cancel);
        }

        public async Task<CommentsPageDTO> GetCommentsAsync(
            string postId,
            string? parentCommentId,
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
            var normalizedParentCommentId = NormalizeOptionalString(parentCommentId);
            var totalCount = await _dbContext.Comments
                .AsNoTracking()
                .CountAsync(c => c.PostId == postId && c.DeletedAt == null && c.ParentCommentId == normalizedParentCommentId, cancel);

            var query = _dbContext.Comments
                .AsNoTracking()
                .Where(c => c.PostId == postId && c.DeletedAt == null && c.ParentCommentId == normalizedParentCommentId);

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

            return new CommentsPageDTO
            {
                Items = items,
                Pagination = new CommentPaginationDTO
                {
                    NextCursor = nextCursor,
                    HasMore = hasMore,
                    Limit = normalizedLimit,
                    TotalCount = totalCount
                }
            };
        }

        // Method help post

        // Method help comment

        public async Task<CommentDTO> UpdateCommentAsync(
            string commentId,
            string userId,
            string content,
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

            await SyncCommentMentionsAsync(comment.Id, ExtractMentionUserIds(content), utcNow, cancel);
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
                CreatedAt = comment.CreatedAt.ToUtc(),
                UpdatedAt = comment.UpdatedAt  == null ? null : comment.UpdatedAt.Value.ToUtc()
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
