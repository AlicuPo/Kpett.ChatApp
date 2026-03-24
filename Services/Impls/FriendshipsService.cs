using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Response.Friend;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Receive;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Kpett.ChatApp.Services.Impls
{
    public class FriendshipServices : IFriendshipService
    {
        private readonly AppDbContext _dbcontext;
        private readonly IRealtimeService _realtimeService;

        public FriendshipServices(AppDbContext dbcontext, IRealtimeService realtimeService)
        {
            _dbcontext = dbcontext;
            _realtimeService = realtimeService;
        }

        public async Task<FriendRequestDTO> CreateFriendRequestAsync(string senderId, string receiverId, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(senderId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Sender ID cannot be empty");

            if (string.IsNullOrWhiteSpace(receiverId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Receiver ID cannot be empty");

            if (senderId == receiverId)
                throw new BadRequestException(ErrorCodes.FRIEND.SELF_REFERENCE, "You cannot send a friend request to yourself.");

            cancel.ThrowIfCancellationRequested();

            var senderExists = await _dbcontext.Users.AnyAsync(u => u.Id == senderId, cancel);
            if (!senderExists)
                throw new NotFoundException(ErrorCodes.FRIEND.SENDER_NOT_FOUND, "Sender user not found");

            var receiverExists = await _dbcontext.Users.AnyAsync(u => u.Id == receiverId, cancel);
            if (!receiverExists)
                throw new NotFoundException(ErrorCodes.FRIEND.RECEIVER_NOT_FOUND, "Receiver user not found");

            var isBlocked = await _dbcontext.Blocks.AnyAsync(b =>
                (b.BlockerId == senderId && b.BlockedId == receiverId) ||
                (b.BlockerId == receiverId && b.BlockedId == senderId),
                cancel);

            if (isBlocked)
                throw new ForbiddenException(ErrorCodes.FRIEND.BLOCKED_RELATIONSHIP, "Cannot send friend request due to block");

            var lowId = string.CompareOrdinal(senderId, receiverId) < 0 ? senderId : receiverId;
            var highId = string.CompareOrdinal(senderId, receiverId) < 0 ? receiverId : senderId;

            var existingFriendship = await _dbcontext.Friendships.FirstOrDefaultAsync(f =>
                f.UserLowId == lowId &&
                f.UserHighId == highId &&
                f.Status == EnumHelper.GetDescription(FriendshipsEnums.Accepted),
                cancel);

            if (existingFriendship != null)
                throw new ConflictException(ErrorCodes.FRIEND.ALREADY_FRIENDS, "Already friends with this user");

            var existingRequest = await _dbcontext.FriendRequests.FirstOrDefaultAsync(fr =>
                ((fr.SenderId == senderId && fr.ReceiverId == receiverId) ||
                 (fr.SenderId == receiverId && fr.ReceiverId == senderId)) &&
                fr.Status == EnumHelper.GetDescription(FriendshipsEnums.Pending),
                cancel);

            if (existingRequest != null)
                throw new ConflictException(ErrorCodes.FRIEND.FRIEND_REQUEST_PENDING, "A friend request already exists between these users");

            var friendRequest = new FriendRequest
            {
                Id = Guid.NewGuid().ToString(),
                SenderId = senderId,
                ReceiverId = receiverId,
                Status = EnumHelper.GetDescription(FriendshipsEnums.Pending),
                CreatedAt = DateTime.UtcNow
            };

            _dbcontext.FriendRequests.Add(friendRequest);
            await _dbcontext.SaveChangesAsync(cancel);

            try
            {
                var sender = await _dbcontext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == senderId, cancel);
                await _realtimeService.PublishAsync($"user:{receiverId}:notifications", new
                {
                    type = "FRIEND_REQUEST_SENT",
                    friendRequestId = friendRequest.Id,
                    senderId,
                    senderName = sender?.DisplayName ?? sender?.Username,
                    senderAvatar = sender?.AvatarUrl,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Real-time notification failed: {ex.Message}");
            }

            return await MapFriendRequestAsync(friendRequest, cancel);
        }

        public async Task<FriendRequestDTO> UpdateFriendRequestStatusAsync(string friendRequestId, string currentUserId, string status, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(friendRequestId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Friend request ID cannot be empty");

            if (string.IsNullOrWhiteSpace(currentUserId))
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");

            if (string.IsNullOrWhiteSpace(status))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Status cannot be empty");

            cancel.ThrowIfCancellationRequested();

            var normalizedStatus = status.Trim().ToLowerInvariant();
            if (normalizedStatus != "accepted" && normalizedStatus != "rejected")
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Status must be either accepted or rejected.");

            var friendRequest = await _dbcontext.FriendRequests.FirstOrDefaultAsync(fr => fr.Id == friendRequestId, cancel);
            if (friendRequest == null)
                throw new NotFoundException(ErrorCodes.FRIEND.FRIEND_REQUEST_NOT_FOUND, "Friend request not found");

            if (friendRequest.ReceiverId != currentUserId)
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "Only the receiver can update this friend request.");

            if (friendRequest.Status != EnumHelper.GetDescription(FriendshipsEnums.Pending))
                throw new ConflictException(ErrorCodes.FRIEND.REQUEST_NOT_FOUND_OR_PROCESSED, "Friend request not found or already processed");

            if (normalizedStatus == "accepted")
            {
                var lowId = string.CompareOrdinal(friendRequest.SenderId, friendRequest.ReceiverId) < 0
                    ? friendRequest.SenderId
                    : friendRequest.ReceiverId;
                var highId = string.CompareOrdinal(friendRequest.SenderId, friendRequest.ReceiverId) < 0
                    ? friendRequest.ReceiverId
                    : friendRequest.SenderId;

                _dbcontext.Friendships.Add(new Friendship
                {
                    UserLowId = lowId,
                    UserHighId = highId,
                    Status = EnumHelper.GetDescription(FriendshipsEnums.Accepted),
                    ActionUserId = currentUserId,
                    CreatedAt = DateTime.UtcNow
                });

                friendRequest.Status = EnumHelper.GetDescription(FriendshipsEnums.Accepted);
                friendRequest.UpdatedAt = DateTime.UtcNow;
                await _dbcontext.SaveChangesAsync(cancel);

                try
                {
                    var receiver = await _dbcontext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUserId, cancel);
                    await _realtimeService.PublishAsync($"user:{friendRequest.SenderId}:notifications", new
                    {
                        type = "FRIEND_REQUEST_ACCEPTED",
                        friendRequestId = friendRequest.Id,
                        acceptedById = currentUserId,
                        acceptedByName = receiver?.DisplayName ?? receiver?.Username,
                        acceptedByAvatar = receiver?.AvatarUrl,
                        timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Real-time notification failed: {ex.Message}");
                }
            }
            else
            {
                friendRequest.Status = EnumHelper.GetDescription(FriendshipsEnums.Rejected);
                friendRequest.UpdatedAt = DateTime.UtcNow;
                await _dbcontext.SaveChangesAsync(cancel);

                try
                {
                    await _realtimeService.PublishAsync($"user:{friendRequest.SenderId}:notifications", new
                    {
                        type = "FRIEND_REQUEST_REJECTED",
                        friendRequestId = friendRequest.Id,
                        rejectedById = currentUserId,
                        timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Real-time notification failed: {ex.Message}");
                }
            }

            return await MapFriendRequestAsync(friendRequest, cancel);
        }

        public async Task CancelFriendRequestAsync(string friendRequestId, string currentUserId, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(friendRequestId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Friend request ID cannot be empty");

            if (string.IsNullOrWhiteSpace(currentUserId))
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");

            cancel.ThrowIfCancellationRequested();

            var friendRequest = await _dbcontext.FriendRequests.FirstOrDefaultAsync(fr => fr.Id == friendRequestId, cancel);
            if (friendRequest == null)
                throw new NotFoundException(ErrorCodes.FRIEND.FRIEND_REQUEST_NOT_FOUND, "Friend request not found");

            if (friendRequest.SenderId != currentUserId)
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "Only the sender can cancel this friend request.");

            if (friendRequest.Status != EnumHelper.GetDescription(FriendshipsEnums.Pending))
                throw new ConflictException(ErrorCodes.FRIEND.REQUEST_NOT_FOUND_OR_PROCESSED, "Friend request not found or already processed");

            friendRequest.Status = EnumHelper.GetDescription(FriendshipsEnums.Cancelled);
            friendRequest.UpdatedAt = DateTime.UtcNow;
            await _dbcontext.SaveChangesAsync(cancel);

            try
            {
                await _realtimeService.PublishAsync($"user:{friendRequest.ReceiverId}:notifications", new
                {
                    type = "FRIEND_REQUEST_CANCELLED",
                    friendRequestId = friendRequest.Id,
                    cancelledById = currentUserId,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Real-time notification failed: {ex.Message}");
            }
        }

        public async Task<List<FriendRequestDTO>> GetPendingFriendRequestsAsync(string userId, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "User ID cannot be empty");

            cancel.ThrowIfCancellationRequested();

            return await _dbcontext.FriendRequests
                .AsNoTracking()
                .Where(fr =>
                    fr.ReceiverId == userId &&
                    fr.Status == EnumHelper.GetDescription(FriendshipsEnums.Pending))
                .Join(
                    _dbcontext.Users.AsNoTracking(),
                    fr => fr.SenderId,
                    u => u.Id,
                    (fr, u) => new FriendRequestDTO
                    {
                        FriendRequestId = fr.Id,
                        SenderId = fr.SenderId,
                        SenderName = u.DisplayName ?? u.Username ?? string.Empty,
                        SenderAvatar = u.AvatarUrl,
                        SenderEmail = u.Email,
                        Status = fr.Status,
                        CreatedAt = fr.CreatedAt
                    })
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancel);
        }

        private async Task<FriendRequestDTO> MapFriendRequestAsync(FriendRequest friendRequest, CancellationToken cancel)
        {
            var sender = await _dbcontext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == friendRequest.SenderId, cancel);

            return new FriendRequestDTO
            {
                FriendRequestId = friendRequest.Id,
                SenderId = friendRequest.SenderId,
                SenderName = sender?.DisplayName ?? sender?.Username ?? string.Empty,
                SenderAvatar = sender?.AvatarUrl,
                SenderEmail = sender?.Email,
                Status = friendRequest.Status,
                CreatedAt = friendRequest.CreatedAt
            };
        }
    }
}
