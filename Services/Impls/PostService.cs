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
    /// <summary>
    /// Service quản lý bài viết: CRUD, feed, nhóm bài viết (uỷ quyền reaction cho <see cref="IPostReactionService"/>).
    /// </summary>
    public class PostService : IPostService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<PostService> _logger;
        private readonly IPostReactionService _postReactionService;

        private readonly string avatarType = UserMediaType.Avatar.GetDescription();
        private static readonly string ApprovedPostStatus = PostStatus.Approved.GetDescription();
        private static readonly string PendingPostStatus = PostStatus.Pending.GetDescription();
        private static readonly string RejectedPostStatus = PostStatus.Rejected.GetDescription();
        private const string ActiveStatus = "active";
        private const string DeletedStatus = "deleted";
        private const string PublicPrivacy = "public";
        private const string AnyonePermission = "anyone";
        private const string AdminModPermission = "admin_mod";
        private const string AdminOnlyPermission = "admin_only";
        private const string AdminRole = "admin";
        private const string ModeratorRole = "moderator";
        private const int DefaultLimit = 10;
        private const int MaxLimit = 50;

        /// <summary>
        /// Khởi tạo service với database context, logger và service reaction.
        /// </summary>
        public PostService(AppDbContext dbContext, ILogger<PostService> logger, IPostReactionService postReactionService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _postReactionService = postReactionService;
        }

        /// <inheritdoc />
        public async Task<PostFeedResponse> CreatePostAsync(string userId, PostRequest postRequest, CancellationToken cancel)
        {
            _logger.LogInformation("User {UserId} creating a new post", userId);

            if (string.IsNullOrWhiteSpace(userId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "User ID cannot be empty");

            if (postRequest == null)
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Post request is required");

            if (!string.IsNullOrWhiteSpace(postRequest.GroupId))
                return await CreateGroupPostAsync(userId, postRequest.GroupId, postRequest, cancel);

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
                GroupId = null,
                Status = ApprovedPostStatus,
                IsNsfw = postRequest.IsNsfw,
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

        /// <inheritdoc />
        public async Task<PostFeedResponse> CreateGroupPostAsync(string userId, string groupId, PostRequest postRequest, CancellationToken cancel)
        {
            _logger.LogInformation("User {UserId} creating a post in group {GroupId}", userId, groupId);

            if (string.IsNullOrWhiteSpace(userId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "User ID cannot be empty");

            if (string.IsNullOrWhiteSpace(groupId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Group ID cannot be empty");

            if (postRequest == null)
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Post request is required");

            var group = await GetActiveGroupAsync(groupId, cancel);
            var member = await GetActiveGroupMemberAsync(group.Id, userId, cancel);
            EnsureCanCreateGroupPost(group, member);

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

            var now = DateTime.UtcNow;
            var status = group.PostApproval && GetGroupRoleRank(group, member) < 1
                ? PendingPostStatus
                : ApprovedPostStatus;

            var newPost = new Post
            {
                Id = Guid.NewGuid().ToString(),
                CreatedByUserId = userId,
                Content = postRequest.Content,
                Privacy = postRequest.Privacy ?? PostPrivacy.Public.GetDescription(),
                Type = PostType.Post.GetDescription(),
                GroupId = group.Id,
                Status = status,
                IsNsfw = postRequest.IsNsfw,
                CreatedAt = now,
                PinnedAt = now,
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

            _logger.LogInformation(
                "User {UserId} created group post {PostId} in group {GroupId} with status {Status}",
                userId,
                newPost.Id,
                group.Id,
                status);

            return BuildPostResponse(newPost, user, mediaResponse, isLiked: false, BuildGroupSummary(group));
        }

        /// <inheritdoc />
        public async Task<PostFeedResponse> UpdateGroupPostStatusAsync(
            string userId,
            string groupId,
            string postId,
            UpdateGroupPostStatusRequest request,
            CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "User ID cannot be empty");

            if (string.IsNullOrWhiteSpace(postId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Post ID cannot be empty");

            if (request == null)
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Status request is required");

            var group = await GetActiveGroupAsync(groupId, cancel);
            var member = await GetActiveGroupMemberAsync(group.Id, userId, cancel);
            if (GetGroupRoleRank(group, member) < 1)
                throw new ForbiddenException(ErrorCodes.GROUP.NOT_ADMIN, "Only group admins or moderators can moderate posts.");

            var normalizedStatus = NormalizePostStatusForWrite(request.Status);

            var post = await _dbContext.Posts
                .FirstOrDefaultAsync(p =>
                    p.Id == postId &&
                    p.GroupId == group.Id &&
                    !p.IsDeleted,
                    cancel);

            if (post == null)
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");

            post.Status = normalizedStatus;
            post.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancel);

            var author = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == post.CreatedByUserId)
                .Select(u => new UserResponse
                {
                    Id = u.Id,
                    Email = u.Email,
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                    IsVerified = u.IsVerified,
                    AvatarUrl = _dbContext.UserMedias
                        .Where(um => um.UserId == u.Id && um.IsPrimary && um.MediaType == avatarType)
                        .Select(um => um.MediaUrl)
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync(cancel)
                ?? throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");

            var media = await _dbContext.PostMedia
                .AsNoTracking()
                .Where(m => m.PostId == post.Id)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new MediaPostResponse
                {
                    PublicId = m.Id,
                    Url = m.MediaUrl,
                    Type = m.MediaType
                })
                .ToListAsync(cancel);

            var isLiked = await _dbContext.PostReactions
                .AsNoTracking()
                .AnyAsync(r => r.PostId == post.Id && r.UserId == userId, cancel);

            return BuildPostResponse(post, author, media, isLiked, BuildGroupSummary(group));
        }

        /// <inheritdoc />
        public async Task<PostFeedResponse> TogglePinPostAsync(
            string userId,
            string groupId,
            string postId,
            CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "User ID cannot be empty");

            if (string.IsNullOrWhiteSpace(postId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Post ID cannot be empty");

            var group = await GetActiveGroupAsync(groupId, cancel);
            var member = await GetActiveGroupMemberAsync(group.Id, userId, cancel);
            if (GetGroupRoleRank(group, member) < 1)
                throw new ForbiddenException(ErrorCodes.GROUP.NOT_ADMIN, "Only group admins or moderators can pin posts.");

            var post = await _dbContext.Posts
                .FirstOrDefaultAsync(p =>
                    p.Id == postId &&
                    p.GroupId == group.Id &&
                    !p.IsDeleted,
                    cancel);

            if (post == null)
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");

            post.IsPinned = !post.IsPinned;
            if (post.IsPinned)
                post.PinnedAt = DateTime.UtcNow;
            post.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancel);

            var author = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == post.CreatedByUserId)
                .Select(u => new UserResponse
                {
                    Id = u.Id,
                    Email = u.Email,
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                    IsVerified = u.IsVerified,
                    AvatarUrl = _dbContext.UserMedias
                        .Where(um => um.UserId == u.Id && um.IsPrimary && um.MediaType == avatarType)
                        .Select(um => um.MediaUrl)
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync(cancel)
                ?? throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");

            var media = await _dbContext.PostMedia
                .AsNoTracking()
                .Where(m => m.PostId == post.Id)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new MediaPostResponse
                {
                    PublicId = m.Id,
                    Url = m.MediaUrl,
                    Type = m.MediaType
                })
                .ToListAsync(cancel);

            var isLiked = await _dbContext.PostReactions
                .AsNoTracking()
                .AnyAsync(r => r.PostId == post.Id && r.UserId == userId, cancel);

            return BuildPostResponse(post, author, media, isLiked, BuildGroupSummary(group));
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public async Task<PostFeedResponse> GetPostByIdAsync(string postId, string? currentUserId, CancellationToken cancel)
        {
            var query = _dbContext.Posts
                .AsNoTracking()
                .Where(p => p.Id == postId && p.IsDeleted == false);

            query = ApplyPostVisibilityFilter(query, currentUserId);

            var post = await query
                .Select(p => new PostFeedResponse
                {
                    Id = p.Id,
                    Content = p.Content,
                    Privacy = p.Privacy,
                    Type = p.Type,
                    GroupId = p.GroupId,
                    Status = p.Status ?? ApprovedPostStatus,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    Group = _dbContext.Groups
                        .Where(g => g.Id == p.GroupId)
                        .Select(g => new PostGroupSummaryResponse
                        {
                            Id = g.Id,
                            Name = g.Name,
                            AvatarUrl = g.AvatarUrl,
                            Privacy = g.Type
                        })
                        .FirstOrDefault(),

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

        /// <inheritdoc />
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

            limit = NormalizeLimit(limit);

            var query = _dbContext.Posts
                .AsNoTracking()
                .Where(p => !p.IsDeleted);

            query = ApplyPostVisibilityFilter(query, currentUserId);

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
                    GroupId = p.GroupId,
                    Status = p.Status ?? ApprovedPostStatus,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    Group = _dbContext.Groups
                        .Where(g => g.Id == p.GroupId)
                        .Select(g => new PostGroupSummaryResponse
                        {
                            Id = g.Id,
                            Name = g.Name,
                            AvatarUrl = g.AvatarUrl,
                            Privacy = g.Type
                        })
                        .FirstOrDefault(),

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

        /// <inheritdoc />
        public async Task<PaginatedData<PostFeedResponse>> GetGroupPostsAsync(
            string? currentUserId,
            string groupId,
            CursorPaginationRequest request,
            string? status = null,
            CancellationToken cancel = default)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Group ID cannot be empty");

            var group = await GetActiveGroupAsync(groupId, cancel);
            var currentMember = string.IsNullOrWhiteSpace(currentUserId)
                ? null
                : await _dbContext.GroupMembers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m =>
                        m.GroupId == group.Id &&
                        m.UserId == currentUserId &&
                        m.Status == ActiveStatus,
                        cancel);

            if (!CanViewGroup(group, currentMember))
                throw new ForbiddenException(ErrorCodes.GROUP.NOT_A_MEMBER, "You are not allowed to view posts in this group.");

            var normalizedLimit = NormalizeLimit(request?.Limit);
            DateTime? cursorDate = null;
            string? cursorId = null;

            if (!string.IsNullOrWhiteSpace(request?.Cursor))
            {
                var decoded = CursorHelper.Decode<BaseCursorPayload>(request.Cursor);
                if (decoded != null)
                {
                    cursorDate = decoded.CreatedAt;
                    cursorId = decoded.Id;
                }
            }

            var canModerate = currentMember != null && GetGroupRoleRank(group, currentMember) >= 1;

            var query = _dbContext.Posts
                .AsNoTracking()
                .Where(p => p.GroupId == group.Id && !p.IsDeleted);

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalizedFilterStatus = NormalizePostStatusForWrite(status);
                if (!canModerate && normalizedFilterStatus != ApprovedPostStatus)
                    throw new ForbiddenException(ErrorCodes.GROUP.NOT_ADMIN, "Only admins can filter by non-approved statuses.");
                query = query.Where(p => p.Status == normalizedFilterStatus);
            }
            else
            {
                query = query.Where(p =>
                    p.Status == null ||
                    p.Status == ApprovedPostStatus ||
                    (!string.IsNullOrWhiteSpace(currentUserId) && p.CreatedByUserId == currentUserId && p.Status == PendingPostStatus) ||
                    (canModerate && p.Status == PendingPostStatus));
            }

            if (cursorDate.HasValue && !string.IsNullOrEmpty(cursorId))
            {
                query = query.Where(p =>
                    p.CreatedAt < cursorDate.Value ||
                    (p.CreatedAt == cursorDate.Value && p.Id.CompareTo(cursorId) < 0));
            }

            var fetchedPosts = await query
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Take(normalizedLimit + 1)
                .Select(p => new PostFeedResponse
                {
                    Id = p.Id,
                    Content = p.Content,
                    Privacy = p.Privacy,
                    Type = p.Type,
                    GroupId = p.GroupId,
                    Status = p.Status ?? ApprovedPostStatus,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    Group = new PostGroupSummaryResponse
                    {
                        Id = group.Id,
                        Name = group.Name,
                        AvatarUrl = group.AvatarUrl,
                        Privacy = group.Type
                    },

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
                        IsPinned = p.IsPinned,
                        CanEdit = p.CreatedByUserId == currentUserId,
                        CanDelete = p.CreatedByUserId == currentUserId || canModerate,
                        CanLike = p.Status == null || p.Status == ApprovedPostStatus,
                        CanComment = p.Status == null || p.Status == ApprovedPostStatus,
                        CanPin = canModerate
                    },
                })
                .AsSplitQuery()
                .ToListAsync(cancel);

            if (!fetchedPosts.Any())
            {
                return new PaginatedData<PostFeedResponse>
                {
                    Items = new List<PostFeedResponse>(),
                    Pagination = new CursorPaginationMeta { Limit = normalizedLimit }
                };
            }

            string? nextCursor = null;

            if (fetchedPosts.Count > normalizedLimit)
            {
                var lastItemInPage = fetchedPosts[normalizedLimit - 1];
                nextCursor = CursorHelper.Encode(new BaseCursorPayload
                {
                    Id = lastItemInPage.Id,
                    CreatedAt = lastItemInPage.CreatedAt
                });
                fetchedPosts = fetchedPosts.Take(normalizedLimit).ToList();
            }

            foreach (var post in fetchedPosts)
            {
                post.CreatedAt = post.CreatedAt.ToUtc();
                post.UpdatedAt = post.UpdatedAt?.ToUtc();
            }

            return new PaginatedData<PostFeedResponse>
            {
                Items = fetchedPosts,
                Pagination = new CursorPaginationMeta
                {
                    NextCursor = nextCursor,
                    Limit = normalizedLimit
                }
            };
        }

        /// <inheritdoc />
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
                .Where(p => p.CreatedByUserId == userId && !p.IsDeleted && p.GroupId == null)
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
                    p.GroupId,
                    p.Status,
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
                GroupId = data.GroupId,
                Status = data.Status ?? ApprovedPostStatus,

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

        /// <inheritdoc />
        public async Task DeletePostAsync(string postId, string userId, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            var post = await _dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId, cancel);

            if (post == null)
            {
                throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");
            }

            if (post.CreatedByUserId != userId && !await CanModerateGroupPostAsync(post, userId, cancel))
            {
                throw new ForbiddenException(ErrorCodes.POST.USER_NOT_AUTHORIZED, "Not authorized to delete this post");
            }

            post.IsDeleted = true;
            post.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation("User {UserId} deleted post with ID {PostId}", userId, postId);

            _dbContext.Posts.Update(post);
            await _dbContext.SaveChangesAsync(cancel);
        }

        /// <inheritdoc />
        public Task<PostReactionDTO> AddReactionAsync(string postId, string userId, byte reactionType, CancellationToken cancel)
            => _postReactionService.AddReactionAsync(postId, userId, reactionType, cancel);

        /// <inheritdoc />
        public Task RemoveReactionAsync(string postId, string userId, CancellationToken cancel)
            => _postReactionService.RemoveReactionAsync(postId, userId, cancel);

        /// <inheritdoc />
        public Task<List<PostReactionDTO>> GetPostReactionsAsync(string postId, CancellationToken cancel)
            => _postReactionService.GetPostReactionsAsync(postId, cancel);

        // HELPER METHODS

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

        private async Task<Group> GetActiveGroupAsync(string groupId, CancellationToken cancel)
        {
            return await _dbContext.Groups
                .AsNoTracking()
                .FirstOrDefaultAsync(g =>
                    g.Id == groupId &&
                    (g.Status == null || g.Status != DeletedStatus),
                    cancel)
                ?? throw new NotFoundException(ErrorCodes.GROUP.NOT_FOUND, "Group not found.");
        }

        private async Task<GroupMember> GetActiveGroupMemberAsync(string groupId, string userId, CancellationToken cancel)
        {
            var member = await _dbContext.GroupMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId, cancel);

            if (member?.Status != ActiveStatus)
                throw new ForbiddenException(ErrorCodes.GROUP.NOT_A_MEMBER, "You are not an active member of this group.");

            return member;
        }

        private void EnsureCanCreateGroupPost(Group group, GroupMember member)
        {
            var rank = GetGroupRoleRank(group, member);
            var permission = NormalizeGroupPostPermission(group.WhoCanPost);

            if (permission == AdminOnlyPermission && rank < 2)
                throw new ForbiddenException(ErrorCodes.GROUP.NOT_ADMIN, "Only group admins can post in this group.");

            if (permission == AdminModPermission && rank < 1)
                throw new ForbiddenException(ErrorCodes.GROUP.NOT_ADMIN, "Only group admins or moderators can post in this group.");
        }

        private static bool CanViewGroup(Group group, GroupMember? member)
        {
            return NormalizeGroupPrivacy(group.Type) == PublicPrivacy || member?.Status == ActiveStatus;
        }

        private static int GetGroupRoleRank(Group group, GroupMember member)
        {
            if (group.OwnerUserId == member.UserId)
                return 2;

            return member.Role?.Trim().ToLowerInvariant() switch
            {
                AdminRole => 2,
                ModeratorRole => 1,
                _ => 0
            };
        }

        private static string NormalizeGroupPrivacy(string? privacy)
        {
            return string.IsNullOrWhiteSpace(privacy)
                ? PublicPrivacy
                : privacy.Trim().ToLowerInvariant();
        }

        private static string NormalizeGroupPostPermission(string? permission)
        {
            return string.IsNullOrWhiteSpace(permission)
                ? AnyonePermission
                : permission.Trim().ToLowerInvariant();
        }

        private static string NormalizePostStatusForWrite(string? status)
        {
            var normalizedStatus = status?.Trim().ToLowerInvariant();

            if (normalizedStatus == ApprovedPostStatus ||
                normalizedStatus == PendingPostStatus ||
                normalizedStatus == RejectedPostStatus)
            {
                return normalizedStatus;
            }

            throw new BadRequestException(ErrorCodes.VALIDATION.INVALID, "Post status must be approved, pending, or rejected.");
        }

        private static int NormalizeLimit(int? limit)
        {
            var value = limit.GetValueOrDefault(DefaultLimit);
            return value <= 0 ? DefaultLimit : Math.Min(value, MaxLimit);
        }

        private static PostGroupSummaryResponse BuildGroupSummary(Group group)
        {
            return new PostGroupSummaryResponse
            {
                Id = group.Id,
                Name = group.Name,
                AvatarUrl = group.AvatarUrl,
                Privacy = group.Type
            };
        }

        private async Task<bool> CanModerateGroupPostAsync(Post post, string userId, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(post.GroupId) || string.IsNullOrWhiteSpace(userId))
                return false;

            var group = await _dbContext.Groups
                .AsNoTracking()
                .FirstOrDefaultAsync(g =>
                    g.Id == post.GroupId &&
                    (g.Status == null || g.Status != DeletedStatus),
                    cancel);

            if (group == null)
                return false;

            var member = await _dbContext.GroupMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(m =>
                    m.GroupId == post.GroupId &&
                    m.UserId == userId &&
                    m.Status == ActiveStatus,
                    cancel);

            return member != null && GetGroupRoleRank(group, member) >= 1;
        }

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
        private PostFeedResponse BuildPostResponse(
            Post post,
            UserResponse author,
            List<MediaPostResponse> mediaResponse,
            bool isLiked,
            PostGroupSummaryResponse? group = null)
        {
            var isApproved = post.Status == null || post.Status == ApprovedPostStatus;

            return new PostFeedResponse
            {
                Id = post.Id,
                Author = author,
                Content = post.Content,
                Type = post.Type,
                GroupId = post.GroupId,
                Group = group,
                Status = post.Status ?? ApprovedPostStatus,
                IsNsfw = post.IsNsfw,
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
                    CanLike = isApproved,
                    CanComment = isApproved
                }
            };
        }
    }
}


