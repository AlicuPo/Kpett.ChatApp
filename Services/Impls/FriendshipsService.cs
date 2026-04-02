using System.Data;
using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Payload.Cursor;
using Kpett.ChatApp.DTOs.Request.Friend;
using Kpett.ChatApp.DTOs.Response.Friend;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Receive;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kpett.ChatApp.Services.Impls
{
    public class FriendshipServices : IFriendshipService
    {
        private static readonly string PendingStatus = EnumHelper.GetDescription(FriendshipsEnums.Pending);
        private static readonly string AcceptedStatus = EnumHelper.GetDescription(FriendshipsEnums.Accepted);
        private static readonly string RejectedStatus = EnumHelper.GetDescription(FriendshipsEnums.Rejected);
        private static readonly string CancelledStatus = EnumHelper.GetDescription(FriendshipsEnums.Cancelled);

        private readonly AppDbContext _dbcontext;
        private readonly IRealtimeService _realtimeService;

        public FriendshipServices(AppDbContext dbcontext, IRealtimeService realtimeService)
        {
            _dbcontext = dbcontext;
            _realtimeService = realtimeService;
        }

        public async Task<CreateFriendRequestResult> CreateFriendRequestAsync(string senderId, string receiverId, CancellationToken cancel)
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

            for (var attempt = 0; attempt < 2; attempt++)
            {
                await using var transaction = await BeginSerializableTransactionAsync(cancel);

                try
                {
                    var operation = await CreateFriendRequestCoreAsync(senderId, receiverId, cancel);
                    await _dbcontext.SaveChangesAsync(cancel);

                    if (transaction != null)
                    {
                        await transaction.CommitAsync(cancel);
                    }

                    var dto = await MapFriendRequestAsync(operation.FriendRequest, cancel);
                    await PublishFriendRequestNotificationAsync(
                        operation.NotificationKind,
                        operation.NotificationUserId,
                        operation.FriendRequest,
                        operation.ActorUserId,
                        cancel);

                    return new CreateFriendRequestResult
                    {
                        FriendRequest = dto,
                        IsCreated = operation.IsCreated
                    };
                }
                catch (DbUpdateException) when (attempt == 0)
                {
                    await RollbackQuietlyAsync(transaction, cancel);
                    _dbcontext.ChangeTracker.Clear();
                }
                catch
                {
                    await RollbackQuietlyAsync(transaction, cancel);
                    throw;
                }
            }

            throw new ConflictException(ErrorCodes.SERVER.DATABASE_ERROR, "Could not create friend request due to a concurrent update.");
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

            for (var attempt = 0; attempt < 2; attempt++)
            {
                await using var transaction = await BeginSerializableTransactionAsync(cancel);

                try
                {
                    var operation = await UpdateFriendRequestStatusCoreAsync(friendRequestId, currentUserId, normalizedStatus, cancel);
                    await _dbcontext.SaveChangesAsync(cancel);

                    if (transaction != null)
                    {
                        await transaction.CommitAsync(cancel);
                    }

                    var dto = await MapFriendRequestAsync(operation.FriendRequest, cancel);
                    await PublishFriendRequestNotificationAsync(
                        operation.NotificationKind,
                        operation.NotificationUserId,
                        operation.FriendRequest,
                        operation.ActorUserId,
                        cancel);

                    return dto;
                }
                catch (DbUpdateException) when (attempt == 0)
                {
                    await RollbackQuietlyAsync(transaction, cancel);
                    _dbcontext.ChangeTracker.Clear();
                }
                catch
                {
                    await RollbackQuietlyAsync(transaction, cancel);
                    throw;
                }
            }

            throw new ConflictException(ErrorCodes.SERVER.DATABASE_ERROR, "Could not update friend request due to a concurrent update.");
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

            if (!HasStatus(friendRequest.Status, PendingStatus))
                throw new ConflictException(ErrorCodes.FRIEND.REQUEST_NOT_FOUND_OR_PROCESSED, "Friend request not found or already processed");

            friendRequest.Status = CancelledStatus;
            friendRequest.UpdatedAt = DateTime.UtcNow;
            await _dbcontext.SaveChangesAsync(cancel);

            await PublishFriendRequestNotificationAsync(
                FriendRequestNotificationKind.Cancelled,
                friendRequest.ReceiverId,
                friendRequest,
                currentUserId,
                cancel);
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
                    fr.Status == PendingStatus)
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

        public async Task<PaginatedData<FriendListItemDTO>> GetFriendsAsync(string currentUserId, FriendListRequest request, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(currentUserId))
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");

            cancel.ThrowIfCancellationRequested();

            var normalizedLimit = request.Limit <= 0 ? 20 : Math.Min(request.Limit, 50);
            var normalizedSearch = request.Search?.Trim();
            var acceptedStatus = EnumHelper.GetDescription(FriendshipsEnums.Accepted);

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

            var friendLinks = _dbcontext.Friendships
                .AsNoTracking()
                .Where(f => f.UserLowId == currentUserId && f.Status == acceptedStatus)
                .Select(f => new
                {
                    FriendId = f.UserHighId,
                    FriendedAt = f.CreatedAt ?? DateTime.MinValue
                })
                .Concat(
                    _dbcontext.Friendships
                        .AsNoTracking()
                        .Where(f => f.UserHighId == currentUserId && f.Status == acceptedStatus)
                        .Select(f => new
                        {
                            FriendId = f.UserLowId,
                            FriendedAt = f.CreatedAt ?? DateTime.MinValue
                        }));

            if (cursorFriendedAt.HasValue && !string.IsNullOrWhiteSpace(cursorFriendId))
            {
                friendLinks = friendLinks.Where(f =>
                    f.FriendedAt < cursorFriendedAt.Value ||
                    (f.FriendedAt == cursorFriendedAt.Value && string.Compare(f.FriendId, cursorFriendId) < 0));
            }

            var query =
                from link in friendLinks
                join user in _dbcontext.Users.AsNoTracking() on link.FriendId equals user.Id
                where user.IsActive
                select new
                {
                    link.FriendId,
                    link.FriendedAt,
                    user.Username,
                    user.DisplayName,
                    user.AvatarUrl,
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

            var items = itemsToProcess
                .Select(friend => new FriendListItemDTO
                {
                    Id = friend.FriendId,
                    Username = friend.Username,
                    DisplayName = friend.DisplayName,
                    AvatarUrl = friend.AvatarUrl,
                    IsVerified = friend.IsVerified,
                    FriendedAt = friend.FriendedAt == DateTime.MinValue ? null : friend.FriendedAt
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

        private async Task<CreateFriendRequestOperationResult> CreateFriendRequestCoreAsync(string senderId, string receiverId, CancellationToken cancel)
        {
            var (lowId, highId) = NormalizePair(senderId, receiverId);

            var existingFriendship = await _dbcontext.Friendships.FirstOrDefaultAsync(f =>
                f.UserLowId == lowId &&
                f.UserHighId == highId &&
                f.Status == AcceptedStatus,
                cancel);

            if (existingFriendship != null)
                throw new ConflictException(ErrorCodes.FRIEND.ALREADY_FRIENDS, "Already friends with this user");

            var existingRequest = await _dbcontext.FriendRequests.FirstOrDefaultAsync(fr =>
                fr.UserLowId == lowId &&
                fr.UserHighId == highId,
                cancel);

            var now = DateTime.UtcNow;
            if (existingRequest == null)
            {
                var friendRequest = new FriendRequest
                {
                    Id = Guid.NewGuid().ToString(),
                    UserLowId = lowId,
                    UserHighId = highId,
                    SenderId = senderId,
                    ReceiverId = receiverId,
                    Status = PendingStatus,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _dbcontext.FriendRequests.Add(friendRequest);

                return new CreateFriendRequestOperationResult(
                    friendRequest,
                    IsCreated: true,
                    FriendRequestNotificationKind.Sent,
                    receiverId,
                    senderId);
            }

            if (HasStatus(existingRequest.Status, AcceptedStatus))
                throw new ConflictException(ErrorCodes.FRIEND.ALREADY_FRIENDS, "Already friends with this user");

            if (HasStatus(existingRequest.Status, PendingStatus))
            {
                if (existingRequest.SenderId == senderId && existingRequest.ReceiverId == receiverId)
                    throw new ConflictException(ErrorCodes.FRIEND.FRIEND_REQUEST_PENDING, "A friend request is already pending.");

                if (existingRequest.SenderId == receiverId && existingRequest.ReceiverId == senderId)
                {
                    existingRequest.Status = AcceptedStatus;
                    existingRequest.UpdatedAt = now;

                    await UpsertAcceptedFriendshipAsync(lowId, highId, senderId, now, cancel);

                    return new CreateFriendRequestOperationResult(
                        existingRequest,
                        IsCreated: false,
                        FriendRequestNotificationKind.Accepted,
                        existingRequest.SenderId,
                        senderId);
                }

                throw new ConflictException(ErrorCodes.FRIEND.FRIEND_REQUEST_PENDING, "A friend request is already pending.");
            }

            if (HasStatus(existingRequest.Status, RejectedStatus) || HasStatus(existingRequest.Status, CancelledStatus))
            {
                existingRequest.SenderId = senderId;
                existingRequest.ReceiverId = receiverId;
                existingRequest.UserLowId = lowId;
                existingRequest.UserHighId = highId;
                existingRequest.Status = PendingStatus;
                existingRequest.UpdatedAt = now;
                existingRequest.CreatedAt ??= now;

                return new CreateFriendRequestOperationResult(
                    existingRequest,
                    IsCreated: false,
                    FriendRequestNotificationKind.Sent,
                    receiverId,
                    senderId);
            }

            throw new ConflictException(ErrorCodes.FRIEND.REQUEST_NOT_FOUND_OR_PROCESSED, "Friend request not found or already processed");
        }

        private async Task<UpdateFriendRequestOperationResult> UpdateFriendRequestStatusCoreAsync(
            string friendRequestId,
            string currentUserId,
            string normalizedStatus,
            CancellationToken cancel)
        {
            var friendRequest = await _dbcontext.FriendRequests.FirstOrDefaultAsync(fr => fr.Id == friendRequestId, cancel);
            if (friendRequest == null)
                throw new NotFoundException(ErrorCodes.FRIEND.FRIEND_REQUEST_NOT_FOUND, "Friend request not found");

            if (friendRequest.ReceiverId != currentUserId)
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "Only the receiver can update this friend request.");

            var now = DateTime.UtcNow;
            if (normalizedStatus == "accepted")
            {
                if (!HasStatus(friendRequest.Status, PendingStatus) && !HasStatus(friendRequest.Status, AcceptedStatus))
                    throw new ConflictException(ErrorCodes.FRIEND.REQUEST_NOT_FOUND_OR_PROCESSED, "Friend request not found or already processed");

                var (lowId, highId) = NormalizePair(friendRequest.SenderId, friendRequest.ReceiverId);
                friendRequest.UserLowId = lowId;
                friendRequest.UserHighId = highId;

                var shouldNotify = !HasStatus(friendRequest.Status, AcceptedStatus);

                await UpsertAcceptedFriendshipAsync(lowId, highId, currentUserId, now, cancel);

                friendRequest.Status = AcceptedStatus;
                friendRequest.UpdatedAt = now;

                return new UpdateFriendRequestOperationResult(
                    friendRequest,
                    shouldNotify ? FriendRequestNotificationKind.Accepted : FriendRequestNotificationKind.None,
                    friendRequest.SenderId,
                    currentUserId);
            }

            if (!HasStatus(friendRequest.Status, PendingStatus))
                throw new ConflictException(ErrorCodes.FRIEND.REQUEST_NOT_FOUND_OR_PROCESSED, "Friend request not found or already processed");

            friendRequest.Status = RejectedStatus;
            friendRequest.UpdatedAt = now;

            return new UpdateFriendRequestOperationResult(
                friendRequest,
                FriendRequestNotificationKind.Rejected,
                friendRequest.SenderId,
                currentUserId);
        }

        private async Task UpsertAcceptedFriendshipAsync(
            string lowId,
            string highId,
            string actionUserId,
            DateTime now,
            CancellationToken cancel)
        {
            var existingFriendship = await _dbcontext.Friendships.FirstOrDefaultAsync(f =>
                f.UserLowId == lowId &&
                f.UserHighId == highId,
                cancel);

            if (existingFriendship == null)
            {
                _dbcontext.Friendships.Add(new Friendship
                {
                    UserLowId = lowId,
                    UserHighId = highId,
                    Status = AcceptedStatus,
                    ActionUserId = actionUserId,
                    CreatedAt = now,
                    UpdatedAt = now
                });

                return;
            }

            existingFriendship.Status = AcceptedStatus;
            existingFriendship.ActionUserId = actionUserId;
            existingFriendship.CreatedAt ??= now;
            existingFriendship.UpdatedAt = now;
        }

        private async Task PublishFriendRequestNotificationAsync(
            FriendRequestNotificationKind notificationKind,
            string notificationUserId,
            FriendRequest friendRequest,
            string actorUserId,
            CancellationToken cancel)
        {
            if (notificationKind == FriendRequestNotificationKind.None || string.IsNullOrWhiteSpace(notificationUserId))
                return;

            try
            {
                if (notificationKind == FriendRequestNotificationKind.Sent)
                {
                    var sender = await _dbcontext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == actorUserId, cancel);
                    await _realtimeService.PublishAsync($"user:{notificationUserId}:notifications", new
                    {
                        type = "FRIEND_REQUEST_SENT",
                        friendRequestId = friendRequest.Id,
                        senderId = actorUserId,
                        senderName = sender?.DisplayName ?? sender?.Username,
                        senderAvatar = sender?.AvatarUrl,
                        timestamp = DateTime.UtcNow
                    });

                    return;
                }

                if (notificationKind == FriendRequestNotificationKind.Accepted)
                {
                    var actor = await _dbcontext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == actorUserId, cancel);
                    await _realtimeService.PublishAsync($"user:{notificationUserId}:notifications", new
                    {
                        type = "FRIEND_REQUEST_ACCEPTED",
                        friendRequestId = friendRequest.Id,
                        acceptedById = actorUserId,
                        acceptedByName = actor?.DisplayName ?? actor?.Username,
                        acceptedByAvatar = actor?.AvatarUrl,
                        timestamp = DateTime.UtcNow
                    });

                    return;
                }

                if (notificationKind == FriendRequestNotificationKind.Rejected)
                {
                    await _realtimeService.PublishAsync($"user:{notificationUserId}:notifications", new
                    {
                        type = "FRIEND_REQUEST_REJECTED",
                        friendRequestId = friendRequest.Id,
                        rejectedById = actorUserId,
                        timestamp = DateTime.UtcNow
                    });

                    return;
                }

                await _realtimeService.PublishAsync($"user:{notificationUserId}:notifications", new
                {
                    type = "FRIEND_REQUEST_CANCELLED",
                    friendRequestId = friendRequest.Id,
                    cancelledById = actorUserId,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Real-time notification failed: {ex.Message}");
            }
        }

        private async Task<IDbContextTransaction?> BeginSerializableTransactionAsync(CancellationToken cancel)
        {
            if (string.Equals(_dbcontext.Database.ProviderName, "Microsoft.EntityFrameworkCore.InMemory", StringComparison.Ordinal))
                return null;

            return await _dbcontext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancel);
        }

        private static async Task RollbackQuietlyAsync(IDbContextTransaction? transaction, CancellationToken cancel)
        {
            if (transaction == null)
                return;

            try
            {
                await transaction.RollbackAsync(cancel);
            }
            catch
            {
            }
        }

        private static (string LowId, string HighId) NormalizePair(string firstUserId, string secondUserId)
        {
            return string.CompareOrdinal(firstUserId, secondUserId) < 0
                ? (firstUserId, secondUserId)
                : (secondUserId, firstUserId);
        }

        private static bool HasStatus(string? actualStatus, string expectedStatus)
        {
            return string.Equals(actualStatus, expectedStatus, StringComparison.OrdinalIgnoreCase);
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

        private sealed record CreateFriendRequestOperationResult(
            FriendRequest FriendRequest,
            bool IsCreated,
            FriendRequestNotificationKind NotificationKind,
            string NotificationUserId,
            string ActorUserId);

        private sealed record UpdateFriendRequestOperationResult(
            FriendRequest FriendRequest,
            FriendRequestNotificationKind NotificationKind,
            string NotificationUserId,
            string ActorUserId);

        private enum FriendRequestNotificationKind
        {
            None,
            Sent,
            Accepted,
            Rejected,
            Cancelled
        }
    }
}
