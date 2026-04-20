using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Request.User;
using Kpett.ChatApp.DTOs.Response.User;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Kpett.ChatApp.Services.Impls
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _dbcontext;
        private readonly IRedisService _redisService;

        public UserService(AppDbContext dbContext, IRedisService redisService)
        {
            _dbcontext = dbContext;
            _redisService = redisService;
        }

        public async Task<UserProfileResponse> GetMyInfo(string userId, CancellationToken cancel)
        {
            var user = await _dbcontext.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new UserProfileResponse
                {
                    Id = u.Id,
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                    AvatarUrl = u.AvatarUrl,
                    IsVerified = u.IsVerified,
                    DayOfBirth = u.DateOfBirth,
                    Biography = u.Biography,
                    Occupation = u.Occupation,
                    Location = u.Location,
                    CoverUrl = u.CoverUrl,
                    CreatedAt = u.CreatedAt
                })
                .FirstOrDefaultAsync(cancel);
            if (user == null)
            {
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");
            }

            user.ViewerContext = new ProfileViewerContext 
            { 
                IsOwner = user.Id == userId,
            };


            return user;
        }

        public async Task<UserResponse> inforUser(UserRequest Request, CancellationToken cancel)
        {
            if (Request.Id == null)
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Id cannot be null");
            }

            var user = await _dbcontext.Users.FindAsync(new object[] { Request.Id }, cancel);
            if (user == null)
            {
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");
            }

            return new UserResponse
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl,
                CreatedAt = user.CreatedAt
            };
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
                    AvatarUrl = u.AvatarUrl,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync(cancel);

            return (users, totalCount);
        }

        public async Task<UserResponse> UpdateUser(string id, string currentUserId, UpdateUserRequest request, CancellationToken cancel)
        {
            if (id != currentUserId)
            {
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You can only update your own profile.");
            }

            var user = await _dbcontext.Users.FindAsync(new object[] { id }, cancel);
            if (user == null)
            {
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");
            }

            if (!string.IsNullOrEmpty(request.DisplayName)) user.DisplayName = request.DisplayName;
            if (!string.IsNullOrEmpty(request.AvatarUrl)) user.AvatarUrl = request.AvatarUrl;
            if (!string.IsNullOrEmpty(request.Phone)) user.Phone = request.Phone;
            if (!string.IsNullOrEmpty(request.Gender)) user.Gender = request.Gender;

            user.UpdatedAt = DateTime.UtcNow;

            await _dbcontext.SaveChangesAsync(cancel);

            return new UserResponse
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl,
                CreatedAt = user.CreatedAt
            };
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
                AvatarUrl = user.AvatarUrl,
                IsVerified = user.IsVerified,
                IsProfileCompleted = true,
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<UserStatsResponse> GetUserStatsAsync(string userId, CancellationToken cancel)
        {
            var userStats = await _dbcontext.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new UserStatsResponse
                {
                    Id = u.Id,
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                    AvatarUrl = u.AvatarUrl,
                    Email = u.Email!,
                    IsVerified = u.IsVerified,
                    IsProfileCompleted = !string.IsNullOrEmpty(u.Username) && !string.IsNullOrEmpty(u.DisplayName),

                    TotalPosts = _dbcontext.Posts.Count(p => p.CreatedByUserId == u.Id && p.IsDeleted == false),

                    Followers = _dbcontext.Follows.Count(f => f.FolloweeId == u.Id),

                    Following = _dbcontext.Follows.Count(f => f.FollowerId == u.Id),

                    CreatedAt = u.CreatedAt,
                })
                .FirstOrDefaultAsync(cancel);

            if (userStats == null)
            {
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");
            }

            return userStats;
        }

        public async Task<UserProfileResponse> GetUserProfileAsync(string targetUsername, string? currentUserId, CancellationToken cancel)
        {
            string pendingStatus = FriendRequestStatus.Pending.ToString();
            bool isGuest = string.IsNullOrEmpty(currentUserId);

            var query = from u in _dbcontext.Users.AsNoTracking()
                        where u.Username == targetUsername && u.Email != null && u.Username != null

                        let isFriend = !isGuest && _dbcontext.Friendships.Any(f =>
                            (f.UserLowId == currentUserId && f.UserHighId == u.Id) ||
                            (f.UserLowId == u.Id && f.UserHighId == currentUserId))

                        let isBlocked = !isGuest && _dbcontext.Blocks.Any(b =>
                            (b.BlockerId == currentUserId && b.BlockedId == u.Id) ||
                            (b.BlockerId == u.Id && b.BlockedId == currentUserId))

                        let pendingRequest = isGuest ? null : _dbcontext.FriendRequests
                            .AsNoTracking()
                            .Where(fr => fr.Status == pendingStatus &&
                                ((fr.SenderId == currentUserId && fr.ReceiverId == u.Id) ||
                                 (fr.SenderId == u.Id && fr.ReceiverId == currentUserId)))
                            .Select(fr => new { fr.Id, fr.SenderId })
                            .FirstOrDefault()

                        select new
                        {
                            User = u,

                            TotalPosts = _dbcontext.Posts.Count(p => p.CreatedByUserId == u.Id && p.IsDeleted == false),
                            Followers = _dbcontext.Follows.Count(f => f.FolloweeId == u.Id),
                            Following = _dbcontext.Follows.Count(f => f.FollowerId == u.Id),
                            Friends = _dbcontext.Friendships.Count(f => f.UserLowId == u.Id || f.UserHighId == u.Id),

                            IsFriend = isFriend,
                            IsBlocked = isBlocked,
                            IsFollowing = !isGuest && _dbcontext.Follows.Any(f => f.FollowerId == currentUserId && f.FolloweeId == u.Id),

                            PendingRequestData = pendingRequest
                        };

            var result = await query
                .AsNoTracking()
                .FirstOrDefaultAsync(cancel);

            if (result == null)
            {
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");
            }

            return new UserProfileResponse
            {
                Id = result.User.Id,
                Username = result.User.Username,
                DisplayName = result.User.DisplayName,
                AvatarUrl = result.User.AvatarUrl,
                IsVerified = result.User.IsVerified,
                IsProfileCompleted = true,

                Biography = result.User.Biography,
                Occupation = result.User.Occupation,
                Location = result.User.Location,
                CoverUrl = result.User.CoverUrl,
                CreatedAt = result.User.CreatedAt,

                Stats = new UserStatsResponse
                {
                    TotalPosts = result.TotalPosts,
                    Followers = result.Followers,
                    Following = result.Following,
                    Friends = result.Friends
                },

                ViewerContext = isGuest ? new ProfileViewerContext
                {
                    IsOwner = false,
                    IsFriend = false,
                    IsFollowing = false,
                    RelationshipRequestId = null,
                    HasSentFriendRequest = false,
                    HasReceivedFriendRequest = false,
                    IsBlocked = false,
                    CanMessage = false
                } : new ProfileViewerContext
                {
                    IsOwner = result.User.Id == currentUserId,
                    IsFriend = result.IsFriend,
                    IsFollowing = result.IsFollowing,
                    IsBlocked = result.IsBlocked,
                    CanMessage = !result.IsBlocked,

                    RelationshipRequestId = result.PendingRequestData?.Id,

                    HasSentFriendRequest = result.PendingRequestData?.SenderId == currentUserId,

                    HasReceivedFriendRequest = result.PendingRequestData?.SenderId == result.User.Id
                },

                IsOnline = await _redisService.IsUserOnlineAsync(result.User.Id)
            };
        }
    }
}
