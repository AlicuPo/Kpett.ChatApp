using Kpett.ChatApp.Constants;
using Kpett.ChatApp.DTOs.Payload.Cursor;
using Kpett.ChatApp.DTOs.Request.Friend;
using Kpett.ChatApp.DTOs.Response.Friend;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.DTOs.Response.User;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Events.Friend;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Extensions;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Threading.Channels;

namespace Kpett.ChatApp.Services.Impls
{
    public class RelationshipService : IRelationshipService
    {
        private readonly AppDbContext _context;
        private readonly IRedisService _redisService;
        private readonly IMediator _mediator;

        public RelationshipService(AppDbContext context, IRedisService redisService, IMediator mediator)
        {
            _context = context;
            _redisService = redisService;
            _mediator = mediator;
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

            // Canonical order để đảm bảo unique index (UserLowId, UserHighId) hoạt động đúng
            // và tránh race condition khi 2 user gửi request đồng thời
            var isOrderCorrect = string.CompareOrdinal(senderId, receiverId) < 0;
            var request = new FriendRequest
            {
                Id = Guid.NewGuid().ToString(),
                SenderId = senderId,
                ReceiverId = receiverId,
                UserLowId = isOrderCorrect ? senderId : receiverId,
                UserHighId = isOrderCorrect ? receiverId : senderId,
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

            await _mediator.Publish(new FriendRequestSentEvent
            {
                RequestId = request.Id,
                SenderId = senderId,
                ReceiverId = receiverId
            });

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

            // Chỉ người nhận mới được phép accept lời mời
            if (request.ReceiverId != currentUserId)
            {
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You are not authorized to accept this friend request.");
            }

            request.Status = FriendRequestStatus.Accepted.GetDescription();
            request.UpdatedAt = DateTime.UtcNow;

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

            var notifiRequest = await _context.Notifications.FirstOrDefaultAsync(n => n.ReferenceId == request.Id);
            if(notifiRequest != null)
            {
                _context.Notifications.Remove(notifiRequest);
            }

            await _context.SaveChangesAsync();

            await _mediator.Publish(new FriendRequestAcceptedEvent
            {
                RequestId = request.Id,
                AccepterId = currentUserId,
                RequesterId = request.SenderId
            });

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

            if (request.ReceiverId != currentUserId)
            {
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You are not authorized to decline this friend request.");
            }

            request.Status = FriendRequestStatus.Declined.GetDescription();

            var follow = await _context.Follows
                .FirstOrDefaultAsync(f => f.FollowerId == request.SenderId && f.FolloweeId == request.ReceiverId);
            if (follow != null)
            {
                _context.Follows.Remove(follow);
            }

            var notifiRequest = await _context.Notifications.FirstOrDefaultAsync(n => n.ReferenceId == request.Id);
            if (notifiRequest != null)
            {
                _context.Notifications.Remove(notifiRequest);
            }

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

            var follow = await _context.Follows.FirstOrDefaultAsync(f => f.FollowerId == request.SenderId && f.FolloweeId == request.ReceiverId);
            if (follow != null)
            {
                _context.Follows.Remove(follow);
            }

            var notifiRequest = await _context.Notifications.FirstOrDefaultAsync(n => n.ReferenceId == request.Id);
            if (notifiRequest != null)
            {
                _context.Notifications.Remove(notifiRequest);
            }

            await _context.SaveChangesAsync();
        }

        public async Task UnfriendAsync(string currentUserId, string targetUserId)
        {
            var activeStatus = FriendshipStatus.Active.GetDescription();
            var acceptedStatus = FriendRequestStatus.Accepted.GetDescription();
            var friendship = await _context.Friendships
                .FirstOrDefaultAsync(f =>
                    ((f.UserLowId == currentUserId && f.UserHighId == targetUserId) ||
                     (f.UserLowId == targetUserId && f.UserHighId == currentUserId))
                    && f.Status == activeStatus);

            if (friendship == null)
            {
                throw new NotFoundException(ErrorCodes.FRIEND.FRIENDSHIP_NOT_FOUND, "Friendship not found or not active.");
            }

            _context.Friendships.Remove(friendship);

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
            var activeStatus = FriendshipStatus.Active.GetDescription();

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

                join userMedia in _context.UserMedias.AsNoTracking()
                    .Where(m => m.IsPrimary == true && m.MediaType == UserMediaType.Avatar.GetDescription())
                    on user.Id equals userMedia.UserId
                    into mediaGroup

                from media in mediaGroup.DefaultIfEmpty()

                where user.IsActive
                select new
                {
                    link.FriendId,
                    link.FriendedAt,
                    user.Username,
                    user.DisplayName,
                    user.IsVerified,
                    AvatarUrl = media != null ? media.MediaUrl : null
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
                    AvatarUrl = friend.AvatarUrl,
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

        public async Task<PaginatedData<UserResponse>> GetFriendsNotInGroupAsync(string currentUserId, GetFriendsNotInGroupRequest request, CancellationToken cancel)
        {
            // VALIDATION & SECURITY CHECK
            if (string.IsNullOrWhiteSpace(currentUserId))
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");

            if (string.IsNullOrWhiteSpace(request.ConversationId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Conversation ID is required.");

            var isMember = await _context.ConversationParticipants
                .AnyAsync(cp => cp.ConversationId == request.ConversationId && cp.UserId == currentUserId, cancel);

            if (!isMember)
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You are not a member of this conversation.");

            var limit = request.Limit <= 0 ? 20 : Math.Min(request.Limit, 50);
            var searchTerm = request.Search?.Trim();
            var activeStatus = FriendshipStatus.Active.GetDescription();

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

            // DATABASE QUERY (ANTI-JOIN)

            var participantsInGroupQuery = _context.ConversationParticipants.AsNoTracking()
                .Where(cp => cp.ConversationId == request.ConversationId && !cp.IsKicked)
                .Select(cp => cp.UserId);

            var myFriendsQuery = _context.Friendships.AsNoTracking()
                .Where(f => (f.UserLowId == currentUserId || f.UserHighId == currentUserId) && f.Status == activeStatus)
                .Select(f => new
                {
                    FriendId = f.UserLowId == currentUserId ? f.UserHighId : f.UserLowId,
                    FriendedAt = f.CreatedAt
                });

            var eligibleFriendsQuery = myFriendsQuery
                .Where(f => !participantsInGroupQuery.Contains(f.FriendId));

            var query = from f in eligibleFriendsQuery
                        join u in _context.Users.AsNoTracking() on f.FriendId equals u.Id
                        where u.IsActive
                        select new
                        {
                            f.FriendId,
                            f.FriendedAt,
                            u.Username,
                            u.DisplayName
                        };

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(x =>
                    (x.DisplayName != null && x.DisplayName.Contains(searchTerm)) ||
                    (x.Username != null && x.Username.Contains(searchTerm)));
            }

            if (cursorFriendedAt.HasValue && !string.IsNullOrWhiteSpace(cursorFriendId))
            {
                query = query.Where(x =>
                    x.FriendedAt < cursorFriendedAt.Value ||
                    (x.FriendedAt == cursorFriendedAt.Value && string.Compare(x.FriendId, cursorFriendId) < 0));
            }

            var rawFriends = await query
                .OrderByDescending(x => x.FriendedAt)
                .ThenByDescending(x => x.FriendId)
                .Take(limit + 1)
                .ToListAsync(cancel);

            // CURSOR PREPARATION
            string? nextCursor = null;
            var itemsToProcess = rawFriends;

            if (rawFriends.Count > limit)
            {
                var lastItem = rawFriends[limit - 1];
                nextCursor = CursorHelper.Encode(new FriendCursorPayload
                {
                    FriendId = lastItem.FriendId,
                    FriendedAt = lastItem.FriendedAt
                });
                itemsToProcess = rawFriends.Take(limit).ToList();
            }

            // POST-PROCESSING (FETCH AVATAR BULK)
            var friendIds = itemsToProcess.Select(i => i.FriendId).ToList();
            var avatarsDict = new Dictionary<string, string>();

            if (friendIds.Any())
            {
                avatarsDict = await _context.UserMedias.AsNoTracking()
                    .Where(m => friendIds.Contains(m.UserId) && m.IsPrimary == true && m.MediaType == UserMediaType.Avatar.GetDescription())
                    .ToDictionaryAsync(m => m.UserId, m => m.MediaUrl, cancel);
            }

            // FINAL MAPPING
            var items = itemsToProcess.Select(f => new UserResponse
            {
                Id = f.FriendId,
                Username = f.Username,
                DisplayName = f.DisplayName ?? f.Username,
                AvatarUrl = avatarsDict.GetValueOrDefault(f.FriendId)
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

        public async Task<List<UserResponse>> GetFriendSuggestionsAsync(string currentUserId, int limit, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(currentUserId))
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");

            var limitToFetch = limit <= 0 ? 10 : Math.Min(limit, 20);

            // Get users I am already friends with
            var activeStatus = FriendshipStatus.Active.GetDescription();
            var friendIds = await _context.Friendships.AsNoTracking()
                .Where(f => (f.UserLowId == currentUserId || f.UserHighId == currentUserId) && f.Status == activeStatus)
                .Select(f => f.UserLowId == currentUserId ? f.UserHighId : f.UserLowId)
                .ToListAsync(cancel);

            // Get users I have pending requests with
            var pendingStatus = FriendRequestStatus.Pending.GetDescription();
            var pendingUserIds = await _context.FriendRequests.AsNoTracking()
                .Where(fr => (fr.SenderId == currentUserId || fr.ReceiverId == currentUserId) && fr.Status == pendingStatus)
                .Select(fr => fr.SenderId == currentUserId ? fr.ReceiverId : fr.SenderId)
                .ToListAsync(cancel);

            var excludedUserIds = new HashSet<string>(friendIds);
            excludedUserIds.UnionWith(pendingUserIds);
            excludedUserIds.Add(currentUserId);

            var query = _context.Users.AsNoTracking()
                .Where(u => u.IsActive && !excludedUserIds.Contains(u.Id))
                .OrderBy(u => Guid.NewGuid()) // Random order
                .Take(limitToFetch);

            var suggestedUsers = await query.Select(u => new
            {
                u.Id,
                u.Username,
                u.DisplayName
            }).ToListAsync(cancel);

            var suggestedUserIds = suggestedUsers.Select(u => u.Id).ToList();

            var avatarsDict = new Dictionary<string, string>();
            if (suggestedUserIds.Any())
            {
                avatarsDict = await _context.UserMedias.AsNoTracking()
                    .Where(m => suggestedUserIds.Contains(m.UserId) && m.IsPrimary == true && m.MediaType == UserMediaType.Avatar.GetDescription())
                    .ToDictionaryAsync(m => m.UserId, m => m.MediaUrl, cancel);
            }

            return suggestedUsers.Select(u => new UserResponse
            {
                Id = u.Id,
                Username = u.Username,
                DisplayName = u.DisplayName ?? u.Username,
                AvatarUrl = avatarsDict.GetValueOrDefault(u.Id)
            }).ToList();
        }
    }
}

