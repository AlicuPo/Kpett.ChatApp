using Kpett.ChatApp.DTOs;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Receive;
using Microsoft.EntityFrameworkCore;

namespace Kpett.ChatApp.Services
{
    public interface IFriendshipsService
    {
        Task RequestFriendRequestAsync(string senderId, string receiverId, CancellationToken cancel);
        Task AcceptFriendRequestAsync(string senderId, string receiverId, CancellationToken cancel);
        Task RejectFriendRequestAsync(string senderId, string receiverId, CancellationToken cancel);
        Task<List<FriendRequestDTO>> GetPendingFriendRequestsAsync(string userId, CancellationToken cancel);
        Task CancelFriendRequestAsync(string senderId, string receiverId, CancellationToken cancel);
    }

    public class FriendshipsServicesImpl : IFriendshipsService
    {
        private readonly AppDbContext _dbcontext;
        private readonly IRealtimeService _realtimeService;
        private readonly INotificationService _notificationService;

        public FriendshipsServicesImpl(AppDbContext dbcontext, IRealtimeService realtimeService, INotificationService notificationService)
        {
            _dbcontext = dbcontext;
            _realtimeService = realtimeService;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Send a friend request from senderId to receiverId
        /// </summary>
        public async Task RequestFriendRequestAsync(string senderId, string receiverId, CancellationToken cancel)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(senderId))
                throw new AppException(StatusCodes.Status400BadRequest, "Sender ID cannot be empty");

            if (string.IsNullOrWhiteSpace(receiverId))
                throw new AppException(StatusCodes.Status400BadRequest, "Receiver ID cannot be empty");

            if (senderId == receiverId)
                throw new AppException(StatusCodes.Status400BadRequest, "Cannot send friend request to yourself");

            cancel.ThrowIfCancellationRequested();

            // Check if users exist
            var senderExists = await _dbcontext.Users.AnyAsync(_ => _.Id == senderId, cancel);
            if (!senderExists)
                throw new AppException(StatusCodes.Status404NotFound, "Sender user not found");

            var receiverExists = await _dbcontext.Users.AnyAsync(_ => _.Id == receiverId, cancel);
            if (!receiverExists)
                throw new AppException(StatusCodes.Status404NotFound, "Receiver user not found");

            // Check if either user is blocked
            var isBlocked = await _dbcontext.Blocks.AnyAsync(_ =>
                (_.BlockerId == senderId && _.BlockedId == receiverId) ||
                (_.BlockerId == receiverId && _.BlockedId == senderId),
                cancel);

            if (isBlocked)
                throw new AppException(StatusCodes.Status403Forbidden, "Cannot send friend request due to block");

            // Check if already friends
            var lowId = string.CompareOrdinal(senderId, receiverId) < 0 ? senderId : receiverId;
            var highId = string.CompareOrdinal(senderId, receiverId) < 0 ? receiverId : senderId;

            var existingFriendship = await _dbcontext.Friendships.FirstOrDefaultAsync(_ =>
                _.UserLowId == lowId &&
                _.UserHighId == highId &&
                _.Status == EnumHelper.GetDescription(FriendshipsEnums.Accepted),
                cancel);

            if (existingFriendship != null)
                throw new AppException(StatusCodes.Status409Conflict, "Already friends with this user");

            // Check for existing pending request (in both directions)
            var existingRequest = await _dbcontext.FriendRequests.FirstOrDefaultAsync(_ =>
                (_.SenderId == senderId && _.ReceiverId == receiverId ||
                 _.SenderId == receiverId && _.ReceiverId == senderId) &&
                _.Status == EnumHelper.GetDescription(FriendshipsEnums.Pending),
                cancel);

            if (existingRequest != null)
                throw new AppException(StatusCodes.Status409Conflict, "A friend request already exists between these users");

            // Create new friend request
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

            // Get sender info for notification
            var sender = await _dbcontext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == senderId, cancel);

            // Send real-time notification
            try
            {
                await _realtimeService.PublishAsync($"user:{receiverId}:notifications", new
                {
                    type = "FRIEND_REQUEST_SENT",
                    friendRequestId = friendRequest.Id,
                    senderId = senderId,
                    senderName = sender?.DisplayName ?? sender?.Name,
                    senderAvatar = sender?.AvatarUrl,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Real-time notification failed: {ex.Message}");
            }

            // Create push notification
            try
            {
                var notificationDTO = new MessageDTO
                {
                    SenderId = senderId,
                    Content = $"{sender?.DisplayName ?? sender?.Name} sent you a friend request"
                };
                await _notificationService.CreateMessageNotificationsAsync(receiverId, senderId, notificationDTO);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Notification creation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Accept a friend request
        /// </summary>
        public async Task AcceptFriendRequestAsync(string senderId, string receiverId, CancellationToken cancel)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(senderId) || string.IsNullOrWhiteSpace(receiverId))
                throw new AppException(StatusCodes.Status400BadRequest, "Sender ID and Receiver ID cannot be empty");

            cancel.ThrowIfCancellationRequested();

            using var transaction = await _dbcontext.Database.BeginTransactionAsync(cancel);

            try
            {
                // Find the pending friend request
                var friendRequest = await _dbcontext.FriendRequests.FirstOrDefaultAsync(_ =>
                    _.SenderId == senderId &&
                    _.ReceiverId == receiverId &&
                    _.Status == EnumHelper.GetDescription(FriendshipsEnums.Pending),
                    cancel);

                if (friendRequest == null)
                    throw new AppException(StatusCodes.Status404NotFound, "Friend request not found or already processed");

                // Create friendship
                var lowId = string.CompareOrdinal(senderId, receiverId) < 0 ? senderId : receiverId;
                var highId = string.CompareOrdinal(senderId, receiverId) < 0 ? receiverId : senderId;

                var friendship = new Friendship
                {
                    UserLowId = lowId,
                    UserHighId = highId,
                    Status = EnumHelper.GetDescription(FriendshipsEnums.Accepted),
                    ActionUserId = receiverId,
                    CreatedAt = DateTime.UtcNow
                };

                _dbcontext.Friendships.Add(friendship);

                // Update friend request status
                friendRequest.Status = EnumHelper.GetDescription(FriendshipsEnums.Accepted);
                friendRequest.UpdatedAt = DateTime.UtcNow;
                _dbcontext.FriendRequests.Update(friendRequest);

                await _dbcontext.SaveChangesAsync(cancel);
                await transaction.CommitAsync(cancel);

                // Get receiver info for notification
                var receiver = await _dbcontext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == receiverId, cancel);

                // Send real-time notification to both users
                try
                {
                    await _realtimeService.PublishAsync($"user:{senderId}:notifications", new
                    {
                        type = "FRIEND_REQUEST_ACCEPTED",
                        friendshipId = $"{lowId}:{highId}",
                        acceptedById = receiverId,
                        acceptedByName = receiver?.DisplayName ?? receiver?.Name,
                        acceptedByAvatar = receiver?.AvatarUrl,
                        timestamp = DateTime.UtcNow
                    });

                    // Notify the acceptor
                    await _realtimeService.PublishAsync($"user:{receiverId}:notifications", new
                    {
                        type = "FRIEND_REQUEST_ACCEPTED_CONFIRMATION",
                        friendshipId = $"{lowId}:{highId}",
                        timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Real-time notification failed: {ex.Message}");
                }

                // Create notification
                try
                {
                    var notificationDTO = new MessageDTO
                    {
                        SenderId = receiverId,
                        Content = $"{receiver?.DisplayName ?? receiver?.Name} accepted your friend request"
                    };
                    await _notificationService.CreateMessageNotificationsAsync(senderId, receiverId, notificationDTO);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Notification creation failed: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancel);
                throw;
            }
        }

        /// <summary>
        /// Reject a friend request
        /// </summary>
        public async Task RejectFriendRequestAsync(string senderId, string receiverId, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(senderId) || string.IsNullOrWhiteSpace(receiverId))
                throw new AppException(StatusCodes.Status400BadRequest, "Sender ID and Receiver ID cannot be empty");

            cancel.ThrowIfCancellationRequested();

            var friendRequest = await _dbcontext.FriendRequests.FirstOrDefaultAsync(_ =>
                _.SenderId == senderId &&
                _.ReceiverId == receiverId &&
                _.Status == EnumHelper.GetDescription(FriendshipsEnums.Pending),
                cancel);

            if (friendRequest == null)
                throw new AppException(StatusCodes.Status404NotFound, "Friend request not found");

            // Update status to rejected
            friendRequest.Status = EnumHelper.GetDescription(FriendshipsEnums.Rejected);
            friendRequest.UpdatedAt = DateTime.UtcNow;
            _dbcontext.FriendRequests.Update(friendRequest);

            await _dbcontext.SaveChangesAsync(cancel);

            // Send real-time notification
            try
            {
                await _realtimeService.PublishAsync($"user:{senderId}:notifications", new
                {
                    type = "FRIEND_REQUEST_REJECTED",
                    rejectedById = receiverId,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Real-time notification failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Cancel a sent friend request
        /// </summary>
        public async Task CancelFriendRequestAsync(string senderId, string receiverId, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(senderId) || string.IsNullOrWhiteSpace(receiverId))
                throw new AppException(StatusCodes.Status400BadRequest, "Sender ID and Receiver ID cannot be empty");

            cancel.ThrowIfCancellationRequested();

            var friendRequest = await _dbcontext.FriendRequests.FirstOrDefaultAsync(_ =>
                _.SenderId == senderId &&
                _.ReceiverId == receiverId &&
                _.Status == EnumHelper.GetDescription(FriendshipsEnums.Pending),
                cancel);

            if (friendRequest == null)
                throw new AppException(StatusCodes.Status404NotFound, "Friend request not found");

            // Update status to cancelled
            friendRequest.Status = EnumHelper.GetDescription(FriendshipsEnums.Cancelled);
            friendRequest.UpdatedAt = DateTime.UtcNow;
            _dbcontext.FriendRequests.Update(friendRequest);

            await _dbcontext.SaveChangesAsync(cancel);

            // Send real-time notification
            try
            {
                await _realtimeService.PublishAsync($"user:{receiverId}:notifications", new
                {
                    type = "FRIEND_REQUEST_CANCELLED",
                    cancelledById = senderId,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Real-time notification failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get all pending friend requests for a user
        /// </summary>
        public async Task<List<FriendRequestDTO>> GetPendingFriendRequestsAsync(string userId, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new AppException(StatusCodes.Status400BadRequest, "User ID cannot be empty");

            cancel.ThrowIfCancellationRequested();

            var requests = await _dbcontext.FriendRequests
                .AsNoTracking()
                .Where(fr =>
                    fr.ReceiverId == userId &&
                    fr.Status == EnumHelper.GetDescription(FriendshipsEnums.Pending))
                .Join(
                    _dbcontext.Users,
                    fr => fr.SenderId,
                    u => u.Id,
                    (fr, u) => new FriendRequestDTO
                    {
                        FriendRequestId = fr.Id,
                        SenderId = fr.SenderId,
                        SenderName = u.DisplayName ?? u.Name,
                        SenderAvatar = u.AvatarUrl,
                        SenderEmail = u.Email,
                        Status = fr.Status,
                        CreatedAt = fr.CreatedAt
                    }
                )
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancel);

            return requests;
        }
    }
}
