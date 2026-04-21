using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Payload.Cursor;
using Kpett.ChatApp.DTOs.Request.Friend;
using Kpett.ChatApp.DTOs.Response.Friend;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Extentions;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Kpett.ChatApp.Services.Impls
{
    public class RelationshipService : IRelationshipService
    {
        private readonly AppDbContext _context;
        private readonly IRedisService _redisService;

        public RelationshipService(AppDbContext context, IRedisService redisService)
        {
            _context = context;
            _redisService = redisService;
        }

        // Friend request
        public async Task<FriendRequestResponse> SendFriendRequestAsync(string senderId, string receiverId)
        {
            if (senderId == receiverId)
            {
                throw new BadRequestException(ErrorCodes.FRIEND.SELF_REFERENCE, "Cannot send friend requests to myself");
            }

            var existingUsersCount = await _context.Users.CountAsync(u => u.Id == senderId || u.Id == receiverId);
            if (existingUsersCount != 2)
            {
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "Sender or Receiver not found");
            }

            var acceptedStatus = FriendRequestStatus.Accepted.GetDescription();
            var pendingStatus = FriendRequestStatus.Pending.GetDescription();

            // Kiểm tra xem đã là bạn bè chưa 
            var isFriend = await _context.Friendships
                .AnyAsync(f => f.UserLowId == senderId && f.UserHighId == receiverId && f.Status == acceptedStatus);

            if (isFriend)
                throw new BadRequestException(ErrorCodes.FRIEND.ALREADY_FRIENDS, "You were already friends.");

            // Kiểm tra xem đã có lời mời Pending giữa 2 người chưa
            var existingRequest = await _context.FriendRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(r => ((r.SenderId == senderId && r.ReceiverId == receiverId) || (r.SenderId == receiverId && r.ReceiverId == senderId)) && r.Status == pendingStatus);

            if (existingRequest != null)
            {
                if (existingRequest.SenderId == senderId)
                {
                    throw new BadRequestException(ErrorCodes.FRIEND.REQUEST_ALREADY_SENT, "You have already sent a friend request to this person.");
                }
                else
                {
                    throw new BadRequestException(ErrorCodes.FRIEND.FRIEND_REQUEST_PENDING, "This person has already sent you a friend request. Please check your pending requests.");
                }
            }

            var request = new FriendRequest
            {
                Id = Guid.NewGuid().ToString(),
                SenderId = senderId,
                ReceiverId = receiverId,
                UserLowId = senderId,
                UserHighId = receiverId,
                Status = pendingStatus,
                CreatedAt = DateTime.UtcNow,
            };
            await _context.FriendRequests.AddAsync(request);

            // Logic gửi lời mời kết bạn = Tự động Follow
            var isFollowing = await _context.Follows.AnyAsync(f => f.FollowerId == senderId && f.FolloweeId == receiverId);
            if (!isFollowing)
            {
                await _context.Follows.AddAsync(new Follow
                {
                    Id = Guid.NewGuid().ToString(),
                    FollowerId = senderId,
                    FolloweeId = receiverId,
                    CreatedAt = DateTime.UtcNow,
                    IsMuted = false
                });
            }

            await _context.SaveChangesAsync();

            return new FriendRequestResponse
            {
                RequestId = request.Id,
                SenderId = request.SenderId,
                ReceiverId = request.ReceiverId,
                Status = request.Status,
                CreatedAt = request.CreatedAt.ToUtc()
            };

        }

        // Accept request
        public async Task AcceptFriendRequestAsync(string currentUserId, string requestId)
        {
            if (!(await _context.Users.AnyAsync(u => u.Id == currentUserId)))
            {
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "Current user not found");
            }

            var pendingStatus = FriendRequestStatus.Pending.GetDescription();
            var request = await _context.FriendRequests
                .FirstOrDefaultAsync(fr => fr.Id == requestId && fr.Status == pendingStatus);

            if (request == null)
            {
                throw new NotFoundException(ErrorCodes.FRIEND.FRIEND_REQUEST_NOT_FOUND, "Friend request not found or not pending.");
            }

            // Cập nhật trạng thái Request
            request.Status = FriendRequestStatus.Accepted.GetDescription();
            request.UpdatedAt = DateTime.UtcNow;

            // Cập nhật hoặc tạo mới Friendship
            var friendship = await _context.Friendships
                 .FirstOrDefaultAsync(f => (f.UserLowId == request.SenderId && f.UserHighId == request.ReceiverId) || (f.UserLowId == request.ReceiverId && f.UserHighId == request.SenderId));

            if (friendship != null)
            {
                friendship.Status = FriendshipStatus.Active.GetDescription();
                friendship.UpdatedAt = DateTime.UtcNow;
                friendship.ActionUserId = request.ReceiverId;
            }
            else
            {
                await _context.Friendships.AddAsync(new Friendship
                {
                    UserLowId = request.SenderId,
                    UserHighId = request.ReceiverId,
                    Status = FriendshipStatus.Active.GetDescription(),
                    ActionUserId = request.ReceiverId,
                    CreatedAt = DateTime.UtcNow,
                });
            }

            // Logic chấp nhận bạn bè = Tự động Follow ngược lại
            var isFollowingBack = await _context.Follows.AnyAsync(f => f.FollowerId == request.ReceiverId && f.FolloweeId == request.SenderId);
            if (!isFollowingBack)
            {
                await _context.Follows.AddAsync(new Follow
                {
                    Id = Guid.NewGuid().ToString(),
                    FollowerId = request.ReceiverId,
                    FolloweeId = request.SenderId,
                    CreatedAt = DateTime.UtcNow,
                    IsMuted = false
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeclineFriendRequestAsync(string currentUserId, string requestId)
        {
            var pendingStatus = FriendRequestStatus.Pending.GetDescription();
            var request = await _context.FriendRequests
                .FirstOrDefaultAsync(fr => fr.Id == requestId && fr.Status == pendingStatus);

            if (request == null)
            {
                throw new NotFoundException(ErrorCodes.FRIEND.FRIEND_REQUEST_NOT_FOUND, "Friend request not found.");
            }

            request.Status = FriendRequestStatus.Declined.GetDescription();

            await _context.SaveChangesAsync();
        }

        public async Task CancelFriendRequestAsync(string currentUserId, string requestId)
        {
            var pendingStatus = FriendRequestStatus.Pending.GetDescription();
            var request = await _context.FriendRequests
                .FirstOrDefaultAsync(fr => fr.Id == requestId && fr.Status == pendingStatus);

            if (request == null)
            {
                throw new NotFoundException(ErrorCodes.FRIEND.FRIEND_REQUEST_NOT_FOUND, "Friend request not found.");
            }

            request.Status = FriendRequestStatus.Cancelled.GetDescription();

            // Hủy Follow
            var follow = await _context.Follows.FirstOrDefaultAsync(f => f.FollowerId == request.SenderId && f.FolloweeId == request.ReceiverId);
            if (follow != null)
            {
                _context.Follows.Remove(follow);
            }

            await _context.SaveChangesAsync();
        }

        public async Task UnfriendAsync(string currentUserId, string targetUserId)
        {
            var activeStatus = FriendshipStatus.Active.GetDescription();
            var acceptedStatus = FriendRequestStatus.Accepted.GetDescription();
            var friendship = await _context.Friendships
                .FirstOrDefaultAsync(f => (f.UserLowId == currentUserId && f.UserHighId == targetUserId) || (f.UserLowId == targetUserId && f.UserHighId == currentUserId) && f.Status == activeStatus);

            if (friendship == null)
            {
                throw new NotFoundException(ErrorCodes.FRIEND.FRIENDSHIP_NOT_FOUND, "Friendship not found or not active.");
            }

            // Xóa quan hệ bạn bè 
            _context.Friendships.Remove(friendship);

            // Hủy bạn bè = Unfollow lẫn nhau
            var follows = await _context.Follows
                .Where(f => (f.FollowerId == currentUserId && f.FolloweeId == targetUserId) ||
                            (f.FollowerId == targetUserId && f.FolloweeId == currentUserId))
                .ToListAsync();

            if (follows.Any())
            {
                _context.Follows.RemoveRange(follows);
            }

            var friendRequest = await _context.FriendRequests
                .FirstOrDefaultAsync(fr => (fr.SenderId == currentUserId && fr.ReceiverId == targetUserId) || (fr.SenderId == targetUserId && fr.ReceiverId == currentUserId) && fr.Status == acceptedStatus);

            if (friendRequest != null)
            {
                friendRequest.Status = FriendRequestStatus.Unfriended.GetDescription();
                friendRequest.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        public async Task FollowAsync(string followerId, string followeeId)
        {
            if (followerId == followeeId)
            {
                throw new BadRequestException(ErrorCodes.FOLLOW.SELF_REFERENCE, "Unable to monitor myself");
            }

            var existingFollow = await _context.Follows
                .FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FolloweeId == followeeId);

            if (existingFollow != null)
            {
                throw new BadRequestException(ErrorCodes.FOLLOW.ALREADY_FOLLOWING, "You are already following this person.");
            }

            var follow = new Follow
            {
                Id = Guid.NewGuid().ToString(),
                FollowerId = followerId,
                FolloweeId = followeeId,
                CreatedAt = DateTime.UtcNow,
                IsMuted = false
            };

            await _context.Follows.AddAsync(follow);
            await _context.SaveChangesAsync();
        }

        public async Task UnfollowAsync(string followerId, string followeeId)
        {
            var follow = await _context.Follows
                .FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FolloweeId == followeeId);

            if (follow == null)
            {
                throw new NotFoundException(ErrorCodes.FOLLOW.FOLLOW_NOT_FOUND, "Follow relationship not found.");
            }

            _context.Follows.Remove(follow);
            await _context.SaveChangesAsync();
        }

        public async Task<PaginatedData<FriendListItemDTO>> GetFriendsAsync(string currentUserId, FriendListRequest request, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(currentUserId))
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");

            var normalizedLimit = request.Limit <= 0 ? 20 : Math.Min(request.Limit, 50);
            var normalizedSearch = request.Search?.Trim();
            var activeStatus = FriendshipStatus.Active.GetDescription(); // Sửa lại: Lấy bạn bè phải là Active, không phải Pending

            DateTime? cursorFriendedAt = null;
            string? cursorFriendId = null;
            if (!string.IsNullOrWhiteSpace(request.Cursor))
            {
                var decoded = CursorHelper.Decode<FriendCursorPayload>(request.Cursor);
                if (decoded != null)
                {
                    cursorFriendedAt = decoded.FriendedAt;
                    cursorFriendId = decoded.FriendId;
                }
            }

            // Tối ưu Query: Không cần Concat, chỉ cần OR điều kiện do chúng ta lưu 1 bản ghi duy nhất
            var friendLinksQuery = _context.Friendships
                .AsNoTracking()
                .Where(f => (f.UserLowId == currentUserId || f.UserHighId == currentUserId) && f.Status == activeStatus)
                .Select(f => new
                {
                    FriendId = f.UserLowId == currentUserId ? f.UserHighId : f.UserLowId,
                    FriendedAt = f.CreatedAt
                });

            if (cursorFriendedAt.HasValue && !string.IsNullOrWhiteSpace(cursorFriendId))
            {
                friendLinksQuery = friendLinksQuery.Where(f =>
                    f.FriendedAt < cursorFriendedAt.Value ||
                    (f.FriendedAt == cursorFriendedAt.Value && string.Compare(f.FriendId, cursorFriendId) < 0));
            }

            var query =
                from link in friendLinksQuery
                join user in _context.Users.AsNoTracking() on link.FriendId equals user.Id
                where user.IsActive
                select new
                {
                    link.FriendId,
                    link.FriendedAt,
                    user.Username,
                    user.DisplayName,
                    user.IsVerified
                };

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                query = query.Where(friend =>
                    (friend.Username != null && friend.Username.Contains(normalizedSearch)) ||
                    (friend.DisplayName != null && friend.DisplayName.Contains(normalizedSearch)));
            }

            var rawFriends = await query
                .OrderByDescending(friend => friend.FriendedAt)
                .ThenByDescending(friend => friend.FriendId)
                .Take(normalizedLimit + 1)
                .ToListAsync(cancel);

            string? nextCursor = null;
            var itemsToProcess = rawFriends;
            if (rawFriends.Count > normalizedLimit)
            {
                var lastItem = rawFriends[normalizedLimit - 1];
                nextCursor = CursorHelper.Encode(new FriendCursorPayload
                {
                    FriendId = lastItem.FriendId,
                    FriendedAt = lastItem.FriendedAt
                });
                itemsToProcess = rawFriends.Take(normalizedLimit).ToList();
            }

            var friendIds = itemsToProcess.Select(i => i.FriendId).ToList();
            var onlineStatuses = await _redisService.GetUsersOnlineStatusAsync(friendIds);

            var items = itemsToProcess
                .Select(friend => new FriendListItemDTO
                {
                    Id = friend.FriendId,
                    Username = friend.Username,
                    DisplayName = friend.DisplayName,
                    IsVerified = friend.IsVerified,
                    FriendedAt = friend.FriendedAt == DateTime.MinValue ? null : friend.FriendedAt,
                    IsOnline = onlineStatuses.TryGetValue(friend.FriendId, out var isOnline) && isOnline
                })
                .ToList();


            return new PaginatedData<FriendListItemDTO>
            {
                Items = items,
                Pagination = new CursorPaginationMeta
                {
                    NextCursor = nextCursor,
                    Limit = normalizedLimit
                }
            };
        }
    }
}