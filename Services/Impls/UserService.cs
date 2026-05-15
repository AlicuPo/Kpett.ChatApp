using Hangfire;
using Kpett.ChatApp.Constants;
using Kpett.ChatApp.DTOs.Payload.Cursor;
using Kpett.ChatApp.DTOs.Request.Post;
using Kpett.ChatApp.DTOs.Request.User;
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
    public class UserService : IUserService
    {
        private readonly AppDbContext _dbcontext;
        private readonly IRedisService _redisService;

        private readonly string AVATAR_TYPE = UserMediaType.Avatar.GetDescription();
        private readonly string COVER_TYPE = UserMediaType.Cover.GetDescription();
        public UserService(AppDbContext dbContext, IRedisService redisService)
        {
            _dbcontext = dbContext;
            _redisService = redisService;
        }

        public async Task<UserGeneralInfoResponse> GetMyGeneralInfo(string userId, CancellationToken cancel)
        {
            var user = await _dbcontext.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new UserGeneralInfoResponse
                {
                    Id = u.Id,
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                    IsVerified = u.IsVerified,
                    AvatarUrl = _dbcontext.UserMedias
                                .Where(um => um.UserId == u.Id && um.MediaType == AVATAR_TYPE && um.IsPrimary)
                                .Select(um => um.MediaUrl)
                                .FirstOrDefault(),
                    CoverUrl = _dbcontext.UserMedias
                                .Where(um => um.UserId == u.Id && um.MediaType == COVER_TYPE && um.IsPrimary)
                                .Select(um => um.MediaUrl)
                                .FirstOrDefault(),
                    DateOfBirth = u.DateOfBirth,
                    Biography = u.Biography,
                    Occupation = u.Occupation,
                    Location = u.Location,
                    CreatedAt = u.CreatedAt
                })
                .FirstOrDefaultAsync(cancel);
            if (user == null)
            {
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");
            }

            if (user.Id != userId)
            {
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You can only access your own profile.");
            }

            return user;
        }

        public async Task<(List<UserResponse>, int)> GetAllUser(UserRequest search, CancellationToken cancel = default)
        {
            var query = _dbcontext.Users.AsQueryable().AsNoTracking();

            if (!string.IsNullOrEmpty(search.Search))
            {
                query = query.Where(u =>
                    u.Username.Contains(search.Search) ||
                    (u.DisplayName != null && u.DisplayName.Contains(search.Search)) ||
                    (u.Email != null && u.Email.Contains(search.Search)));
            }

            var totalCount = await query.CountAsync(cancel);

            var users = await query
                .Skip((search.Page - 1) * search.PageSize)
                .Take(search.PageSize)
                .Select(u => new UserResponse
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    DisplayName = u.DisplayName,
                    AvatarUrl = _dbcontext.UserMedias
                                .Where(um => um.UserId == u.Id && um.MediaType == AVATAR_TYPE && um.IsPrimary)
                                .Select(um => um.MediaUrl)
                                .FirstOrDefault(),
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync(cancel);

            return (users, totalCount);
        }

        public async Task<UserGeneralInfoResponse> UpdateUserGeneralInfo(string currentUserId, UpdateGeneralInfoUserRequest request, CancellationToken cancel)
        {
            if (string.IsNullOrEmpty(request.Username))
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Username is required");
            }

            if (string.IsNullOrEmpty(request.DisplayName))
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "DisplayName is required");
            }

            var user = await _dbcontext.Users.FirstOrDefaultAsync(u => u.Id == currentUserId, cancel);
            if (user == null)
            {
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");
            }

            if (request.Username != user.Username)
            {
                if (await _dbcontext.Users.AnyAsync(u => u.Username == request.Username && u.Id != currentUserId, cancel))
                {
                    throw new BadRequestException(ErrorCodes.USER.USERNAME_TAKEN, "Username is already taken");
                }
                user.Username = request.Username;
            }

            user.DisplayName = request.DisplayName;
            user.Occupation = request.Occupation;
            user.Location = request.Location;
            user.Biography = request.Biography;
            user.DateOfBirth = request.DateOfBirth;
            user.UpdatedAt = DateTime.UtcNow;

            await _dbcontext.SaveChangesAsync();

            return new UserGeneralInfoResponse
            {
                Id = user.Id,
                Username = user.Username,
                DisplayName = user.DisplayName,
                Occupation = user.Occupation,
                Location = user.Location,
                DateOfBirth = user.DateOfBirth,
                Biography = user.Biography,
                CreatedAt = user.CreatedAt.ToUtc(),
                UpdatedAt = user.UpdatedAt?.ToUtc()
            };
        }

        public async Task<UserMediaResponse> UpdateUserMedia(string currentUserId, MediaRequest media, string mediaType)
        {
            if (media == null)
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Media is not null");
            }

            var currrentUserMediaPrimary = await _dbcontext.UserMedias
                .FirstOrDefaultAsync(um => um.UserId == currentUserId && um.IsPrimary && um.MediaType == mediaType);
            if (currrentUserMediaPrimary != null)
            {
                currrentUserMediaPrimary.IsPrimary = false;
                currrentUserMediaPrimary.UpdatedAt = DateTime.UtcNow;
                _dbcontext.UserMedias.Update(currrentUserMediaPrimary);
            }

            var userMedia = new UserMedia
            {
                Id = media.PublicId,
                UserId = currentUserId,
                IsPrimary = true,
                MediaUrl = media.Url,
                IsTemporary = false,
                MediaType = mediaType,
                MimeType = media.Type,
                CreatedAt = DateTime.UtcNow
            };

            await _dbcontext.UserMedias.AddAsync(userMedia);
            await _dbcontext.SaveChangesAsync();

            BackgroundJob.Enqueue<IMediaService>(e => e.ConfirmMediaOnCloudinaryAsync(new List<string> { userMedia.Id }));

            return new UserMediaResponse
            {
                Id = userMedia.Id,
                IsPrimary = userMedia.IsPrimary,
                MediaType = userMedia.MediaType,
                MimeType = userMedia.MediaType,
                Url = userMedia.MediaUrl,
                CreatedAt = userMedia.CreatedAt.ToUtc(),
                UpdatedAt = userMedia.UpdatedAt.ToUtc()
            };
        }

        public async Task<bool> DeleteUserMediaPrimaryAsync(string currentUserId, string mediaType)
        {
            if (_dbcontext.Users.AnyAsync(u => u.Id == currentUserId).Result == false)
            {
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");
            }

            var userMedia = await _dbcontext.UserMedias
                .FirstOrDefaultAsync(um => um.UserId == currentUserId && um.IsPrimary && um.MediaType == mediaType);
            if (userMedia == null)
            {
                throw new BadRequestException(ErrorCodes.MEDIA.NOT_FOUND, "Media not found or does not belong to the user");
            }

            if (userMedia.IsPrimary)
            {
                userMedia.IsPrimary = false;
                userMedia.UpdatedAt = DateTime.UtcNow;

                _dbcontext.UserMedias.Update(userMedia);
                await _dbcontext.SaveChangesAsync();
            }

            return true;
        }

        public async Task<bool> DeleteUser(string id, string currentUserId, CancellationToken cancel)
        {
            if (id != currentUserId)
            {
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You can only delete your own profile.");
            }

            var user = await _dbcontext.Users.FindAsync(new object[] { id }, cancel);
            if (user == null)
            {
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");
            }

            user.IsActive = false;

            await _dbcontext.SaveChangesAsync(cancel);
            return true;
        }

        public async Task<UsernameCheckResponse> CheckExistByUsername(string username, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Username cannot be null or empty");
            }

            var normalizedUsername = username.Trim().ToLower();

            bool exists = await _dbcontext.Users.AnyAsync(u => u.Username.ToLower() == normalizedUsername);

            return new UsernameCheckResponse
            {
                IsAvailable = !exists
            };
        }

        public async Task<UserResponse> AccountSetup(string userId, AccountSetupRequest accountSetupRequest, CancellationToken cancel)
        {
            var user = await _dbcontext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancel);
            if (user == null)
            {
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");
            }

            if (string.IsNullOrEmpty(accountSetupRequest.Username) || string.IsNullOrEmpty(accountSetupRequest.DisplayName))
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Username and DisplayName are required");
            }

            if (await _dbcontext.Users.AnyAsync(u => u.Username == accountSetupRequest.Username && u.Id != userId, cancel))
            {
                throw new BadRequestException(ErrorCodes.USER.USERNAME_TAKEN, "Username is already taken");
            }

            user.Username = accountSetupRequest.Username;
            user.DisplayName = accountSetupRequest.DisplayName;
            user.Biography = accountSetupRequest.Biography;
            user.Interests = string.Join(",", accountSetupRequest.Interests);

            await _dbcontext.SaveChangesAsync();

            return new UserResponse
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email!,
                DisplayName = user.DisplayName,
                IsVerified = user.IsVerified,
                IsProfileCompleted = true,
                AvatarUrl = _dbcontext.UserMedias
                            .Where(um => um.UserId == user.Id && um.MediaType == AVATAR_TYPE && um.IsPrimary)
                            .Select(um => um.MediaUrl)
                            .FirstOrDefault(),
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<UserWithStatResponse> GetUserStatsAsync(string userId, CancellationToken cancel)
        {
            var userStats = await _dbcontext.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new UserWithStatResponse
                {
                    Id = u.Id,
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                    Email = u.Email!,
                    IsVerified = u.IsVerified,
                    IsProfileCompleted = !string.IsNullOrEmpty(u.Username) && !string.IsNullOrEmpty(u.DisplayName),
                    AvatarUrl = _dbcontext.UserMedias
                                .Where(um => um.UserId == u.Id && um.MediaType == AVATAR_TYPE && um.IsPrimary)
                                .Select(um => um.MediaUrl)
                                .FirstOrDefault(),
                    Stats = new UserStatsResponse
                    {
                        TotalPosts = _dbcontext.Posts.Count(p => p.CreatedByUserId == u.Id && p.IsDeleted == false),
                        Followers = _dbcontext.Follows.Count(f => f.FolloweeId == u.Id),
                        Following = _dbcontext.Follows.Count(f => f.FollowerId == u.Id),
                        Friends = _dbcontext.Friendships.Count(f => f.UserLowId == u.Id || f.UserHighId == u.Id)
                    },

                    CreatedAt = u.CreatedAt,
                })
                .FirstOrDefaultAsync(cancel);

            if (userStats == null)
            {
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");
            }

            userStats.CreatedAt = userStats.CreatedAt.ToUtc();

            return userStats;
        }

        public async Task<UserProfileResponse> GetUserProfileAsync(string targetUsername, string? currentUserId, CancellationToken cancel)
        {
            bool isGuest = string.IsNullOrEmpty(currentUserId);

            string pendingStatus = FriendRequestStatus.Pending.ToString();

            var query = _dbcontext.Users
                .AsNoTracking()
                .Where(u => u.Username == targetUsername && u.Email != null)
                .Select(u => new
                {
                    User = new
                    {
                        u.Id,
                        u.Email,
                        u.Username,
                        u.DisplayName,
                        u.IsVerified,
                        u.Biography,
                        u.Occupation,
                        u.Location,
                        u.CreatedAt
                    },

                    AvatarUrl = _dbcontext.UserMedias
                                .Where(um => um.UserId == u.Id && um.MediaType == AVATAR_TYPE && um.IsPrimary)
                                .Select(um => um.MediaUrl)
                                .FirstOrDefault(),

                    CoverUrl = _dbcontext.UserMedias
                                .Where(um => um.UserId == u.Id && um.MediaType == COVER_TYPE && um.IsPrimary)
                                .Select(um => um.MediaUrl)
                                .FirstOrDefault(),

                    TotalPosts = _dbcontext.Posts.Count(p => p.CreatedByUserId == u.Id && !p.IsDeleted),
                    Followers = _dbcontext.Follows.Count(f => f.FolloweeId == u.Id),
                    Following = _dbcontext.Follows.Count(f => f.FollowerId == u.Id),
                    Friends = _dbcontext.Friendships.Count(f => f.UserLowId == u.Id || f.UserHighId == u.Id),

                    IsFriend = !isGuest && _dbcontext.Friendships.Any(f =>
                        (f.UserLowId == currentUserId && f.UserHighId == u.Id) ||
                        (f.UserLowId == u.Id && f.UserHighId == currentUserId)),

                    IsBlocked = !isGuest && _dbcontext.Blocks.Any(b =>
                        (b.BlockerId == currentUserId && b.BlockedId == u.Id) ||
                        (b.BlockerId == u.Id && b.BlockedId == currentUserId)),

                    IsFollowing = !isGuest && _dbcontext.Follows.Any(f =>
                        f.FollowerId == currentUserId && f.FolloweeId == u.Id),

                    PendingRequest = isGuest ? null : _dbcontext.FriendRequests
                        .Where(fr => fr.Status == pendingStatus &&
                            ((fr.SenderId == currentUserId && fr.ReceiverId == u.Id) ||
                             (fr.SenderId == u.Id && fr.ReceiverId == currentUserId)))
                        .Select(fr => new { fr.Id, fr.SenderId })
                        .FirstOrDefault()
                });

            var result = await query.FirstOrDefaultAsync(cancel);

            if (result == null)
            {
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");
            }

            bool isOnline = await _redisService.IsUserOnlineAsync(result.User.Id);

            return new UserProfileResponse
            {
                Id = result.User.Id,
                Username = result.User.Username,
                DisplayName = result.User.DisplayName,
                IsVerified = result.User.IsVerified,
                IsProfileCompleted = true,

                Biography = result.User.Biography,
                Occupation = result.User.Occupation,
                Location = result.User.Location,
                CreatedAt = result.User.CreatedAt,

                AvatarUrl = result.AvatarUrl,
                CoverUrl = result.CoverUrl,

                Stats = new UserStatsResponse
                {
                    TotalPosts = result.TotalPosts,
                    Followers = result.Followers,
                    Following = result.Following,
                    Friends = result.Friends
                },

                ViewerContext = isGuest ? new UserProfileViewerContextResponse
                {
                    IsOwner = false,
                    IsFriend = false,
                    IsFollowing = false,
                    RelationshipRequestId = null,
                    HasSentFriendRequest = false,
                    HasReceivedFriendRequest = false,
                    IsBlocked = false,
                    CanMessage = false
                } : new UserProfileViewerContextResponse
                {
                    IsOwner = result.User.Id == currentUserId,
                    IsFriend = result.IsFriend,
                    IsFollowing = result.IsFollowing,
                    IsBlocked = result.IsBlocked,
                    CanMessage = !result.IsBlocked,
                    RelationshipRequestId = result.PendingRequest?.Id,
                    HasSentFriendRequest = result.PendingRequest?.SenderId == currentUserId,
                    HasReceivedFriendRequest = result.PendingRequest?.SenderId == result.User.Id
                },

                IsOnline = isOnline
            };
        }

        public async Task<PaginatedData<UserResponse>> SearchUsersAsync(string? currentUserId, string keyword, int limit, string? cursor, CancellationToken cancel)
        {
            limit = limit <= 0 ? 20 : Math.Min(limit, 50);
            var searchTerm = keyword?.Trim() ?? string.Empty;

            // Khởi tạo Query cơ bản (Bỏ qua tracking để tối ưu tốc độ đọc)
            var query = _dbcontext.Users.AsNoTracking().AsQueryable();

            // Lọc theo từ khóa (Tìm trong DisplayName hoặc Username)
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(u =>
                    (u.DisplayName != null && u.DisplayName.Contains(searchTerm)) ||
                    (u.Username != null && u.Username.Contains(searchTerm))
                );
            }

            // Loại trừ người đang thực hiện tìm kiếm
            if (!string.IsNullOrWhiteSpace(currentUserId))
            {
                query = query.Where(u => u.Id != currentUserId);
            }

            // Giải mã Cursor
            string? cursorId = null;
            if (!string.IsNullOrWhiteSpace(cursor))
            {
                var decoded = CursorHelper.Decode<UserSearchCursorPayload>(cursor);
                if (decoded != null) cursorId = decoded.UserId;
            }

            // Áp dụng Cursor Pagination (Sắp xếp tăng dần theo Id)
            if (!string.IsNullOrWhiteSpace(cursorId))
            {
                query = query.Where(u => string.Compare(u.Id, cursorId) > 0);
            }

            // Truy vấn dữ liệu từ DB (Dư 1 record để check NextCursor)
            var rawUsers = await query
                .OrderBy(u => u.Id)
                .Take(limit + 1)
                .Select(u => new
                {
                    u.Id,
                    u.DisplayName,
                    u.Username,
                    // Sub-query để lấy Avatar một cách tối ưu
                    AvatarUrl = _dbcontext.UserMedias
                        .Where(um => um.UserId == u.Id && um.IsPrimary && um.MediaType == "Avatar")
                        .Select(um => um.MediaUrl)
                        .FirstOrDefault()
                })
                .ToListAsync(cancel);

            // Xử lý phân trang
            string? nextCursor = null;
            if (rawUsers.Count > limit)
            {
                var lastItem = rawUsers[limit - 1];
                nextCursor = CursorHelper.Encode(new UserSearchCursorPayload { UserId = lastItem.Id });
                rawUsers.RemoveAt(limit);
            }

            // Mapping sang DTO trả về
            var items = rawUsers.Select(u => new UserResponse
            {
                Id = u.Id,
                DisplayName = u.DisplayName ?? u.Username,
                Username = u.Username,
                AvatarUrl = u.AvatarUrl
            }).ToList();

            return new PaginatedData<UserResponse>
            {
                Items = items,
                Pagination = new CursorPaginationMeta
                {
                    NextCursor = nextCursor,
                    Limit = limit
                }
            };
        }
    }
}

