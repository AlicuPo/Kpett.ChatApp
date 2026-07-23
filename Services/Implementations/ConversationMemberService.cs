using Kpett.ChatApp.Data;
using Kpett.ChatApp.Constants;
using Kpett.ChatApp.Extensions;
using Kpett.ChatApp.DTOs.Payload.Cursor;
using Kpett.ChatApp.DTOs.Request.Conversation;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.DTOs.Response.Conversation;
using Kpett.ChatApp.DTOs.Response.Conversation.Metadata;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.DTOs.Response.User;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Helpers;
using Kpett.ChatApp.Hubs;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Abstractions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Kpett.ChatApp.Services.Implementations
{
    /// <summary>Service quản lý thành viên hội thoại nhóm: thêm, xoá, lấy danh sách.</summary>
    public class ConversationMemberService : IConversationMemberService
    {
        private readonly AppDbContext _context;
        private readonly IRedisService _redisService;
        private readonly IHubContext<AppHub> _chatHubContext;
        private readonly ILogger<ConversationMemberService> _logger;

        private static readonly JsonSerializerOptions _jsonCamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        /// <summary>Khởi tạo service với các dependencies.</summary>
        public ConversationMemberService(AppDbContext dbContext, IRedisService redisService, IHubContext<AppHub> chatHubContext, ILogger<ConversationMemberService> logger)
        {
            _context = dbContext;
            _redisService = redisService;
            _chatHubContext = chatHubContext;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<bool> AddMembersToGroupAsync(string currentUserId, AddMembersRequest request, CancellationToken cancel)
        {
            _logger.LogInformation("User {UserId} is adding {UserCount} members to conversation {ConversationId}", currentUserId, request.UserIdsToAdd?.Count ?? 0, request.ConversationId);

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                _logger.LogWarning("Add members rejected because current user ID is empty");
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");
            }
            if (request.UserIdsToAdd == null || !request.UserIdsToAdd.Any())
            {
                _logger.LogWarning("Add members rejected for user {UserId} because no user IDs were provided", currentUserId);
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "User IDs to add are required.");
            }

            var uniqueUserIdsToAdd = request.UserIdsToAdd.Where(id => !string.IsNullOrWhiteSpace(id) && id != currentUserId).Distinct().ToList();
            if (!uniqueUserIdsToAdd.Any())
            {
                _logger.LogWarning("Add members rejected for user {UserId} in conversation {ConversationId} because no valid users remain after normalization", currentUserId, request.ConversationId);
                throw new BadRequestException(ErrorCodes.VALIDATION.INVALID, "No valid users to add.");
            }

            var conversation = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == request.ConversationId && c.Type == "Group", cancel);
            if (conversation == null)
            {
                _logger.LogWarning("Add members rejected because group conversation {ConversationId} was not found", request.ConversationId);
                throw new NotFoundException(ErrorCodes.CONVERSATION.NOT_FOUND, "Group conversation not found.");
            }

            var currentUserParticipant = await _context.ConversationParticipants.FirstOrDefaultAsync(cp => cp.ConversationId == request.ConversationId && cp.UserId == currentUserId && !cp.IsKicked, cancel);
            if (currentUserParticipant == null)
            {
                _logger.LogWarning("User {UserId} attempted to add members to conversation {ConversationId} without active membership", currentUserId, request.ConversationId);
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You are not an active member of this group.");
            }

            var existingParticipants = await _context.ConversationParticipants
                .Where(cp => cp.ConversationId == request.ConversationId && uniqueUserIdsToAdd.Contains(cp.UserId))
                .ToListAsync(cancel);

            var activeMemberIds = existingParticipants.Where(cp => !cp.IsKicked).Select(cp => cp.UserId).ToHashSet();
            var userIdsToProcess = uniqueUserIdsToAdd.Where(id => !activeMemberIds.Contains(id)).ToList();

            if (!userIdsToProcess.Any())
            {
                _logger.LogWarning("Add members rejected for conversation {ConversationId} because all provided users are already active members", request.ConversationId);
                throw new BadRequestException(ErrorCodes.VALIDATION.INVALID, "All provided users are already active members in the group.");
            }

            var usersDictionary = await _context.Users.AsNoTracking()
            .Where(u => userIdsToProcess.Contains(u.Id) || u.Id == currentUserId)
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName ?? u.Username, cancel);

            if (usersDictionary.Count(x => userIdsToProcess.Contains(x.Key)) != userIdsToProcess.Count)
            {
                _logger.LogWarning("Add members rejected for conversation {ConversationId} because one or more target users were not found", request.ConversationId);
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "One or more users to add do not exist.");
            }

            var now = DateTime.UtcNow;
            string memberRole = ConversationRole.Member.GetDescription();

            foreach (var participant in existingParticipants.Where(cp => cp.IsKicked && userIdsToProcess.Contains(cp.UserId)))
            {
                participant.IsKicked = false; participant.JoinedAt = now; participant.LastReadAt = DateTime.MinValue; participant.Role = memberRole;
            }

            var reactivatedUserIds = existingParticipants.Select(p => p.UserId).ToHashSet();
            var newParticipants = userIdsToProcess.Where(id => !reactivatedUserIds.Contains(id)).Select(userId => new ConversationParticipant
            {
                Id = Guid.NewGuid().ToString(),
                ConversationId = request.ConversationId,
                UserId = userId,
                JoinedAt = now,
                LastReadAt = DateTime.MinValue,
                Role = memberRole
            }).ToList();

            if (newParticipants.Any()) _context.ConversationParticipants.AddRange(newParticipants);

            string adderName = usersDictionary.GetValueOrDefault(currentUserId, "Một thành viên");
            string addedNamesString = string.Join(", ", userIdsToProcess.Select(id => usersDictionary[id]));

            var systemMetadata = new MemberAddedMetadata
            {
                Actor = new SnapshotUser { Id = currentUserId, Name = adderName },
                Targets = userIdsToProcess.Select(id => new SnapshotUser { Id = id, Name = usersDictionary[id] }).ToList()
            };

            var systemMessage = new Message
            {
                Id = Guid.NewGuid().ToString(),
                ConversationId = request.ConversationId,
                SenderId = currentUserId,
                Type = MessageType.System.GetDescription(),
                Content = $"{adderName} đã thêm {addedNamesString} vào nhóm.",
                Metadata = JsonSerializer.Serialize(systemMetadata, _jsonCamelCase),
                CreatedAt = now
            };

            _context.Messages.Add(systemMessage);

            conversation.LastMessageAt = now;
            currentUserParticipant.LastReadMessageId = systemMessage.Id;
            currentUserParticipant.LastReadAt = now;

            _context.Conversations.Update(conversation);
            _context.ConversationParticipants.Update(currentUserParticipant);

            await _context.SaveChangesAsync(cancel);

            var activeIds = await _context.ConversationParticipants.AsNoTracking().Where(p => p.ConversationId == request.ConversationId && !p.IsKicked).Select(p => p.UserId).ToListAsync(cancel);
            var msgResponse = await MapToMessageResponseAsync(systemMessage.Id, currentUserId, cancel);

            try
            {
                await _chatHubContext.Clients.Users(activeIds).SendAsync("ReceiveNewMessage", msgResponse, cancel);
                await _chatHubContext.Clients.Users(userIdsToProcess).SendAsync("AddedToConversation", request.ConversationId, cancel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR Error AddMembersToGroupAsync");
            }

            _logger.LogInformation("User {UserId} added {UserCount} members to conversation {ConversationId}", currentUserId, userIdsToProcess.Count, request.ConversationId);
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> RemoveMemberFromGroupAsync(string currentUserId, string conversationId, string userIdToRemove, CancellationToken cancel)
        {
            _logger.LogInformation("User {UserId} is removing user {TargetUserId} from conversation {ConversationId}", currentUserId, userIdToRemove, conversationId);

            if (string.IsNullOrWhiteSpace(currentUserId) || string.IsNullOrWhiteSpace(userIdToRemove))
            {
                _logger.LogWarning("Remove member rejected for conversation {ConversationId} because user info is invalid", conversationId);
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "Invalid user info.");
            }

            var conversation = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId && c.Type == "Group", cancel);
            if (conversation == null)
            {
                _logger.LogWarning("Remove member rejected because group conversation {ConversationId} was not found", conversationId);
                throw new NotFoundException(ErrorCodes.CONVERSATION.NOT_FOUND, "Group conversation not found.");
            }

            var participants = await _context.ConversationParticipants
            .Where(cp => cp.ConversationId == conversationId && (cp.UserId == currentUserId || cp.UserId == userIdToRemove))
            .ToListAsync(cancel);

            var currentUserParticipant = participants.FirstOrDefault(cp => cp.UserId == currentUserId);
            if (currentUserParticipant == null)
            {
                _logger.LogWarning("User {UserId} attempted to remove member from conversation {ConversationId} without membership", currentUserId, conversationId);
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You are not a member of this group.");
            }

            var targetParticipant = participants.FirstOrDefault(cp => cp.UserId == userIdToRemove);
            if (targetParticipant == null)
            {
                _logger.LogWarning("Remove member rejected because user {TargetUserId} is not in conversation {ConversationId}", userIdToRemove, conversationId);
                throw new NotFoundException(ErrorCodes.CONVERSATION.NOT_FOUND, "The target user is not a member of this group.");
            }

            var usersInfo = await BaseUserProjectionQuery().Where(u => u.Id == currentUserId || u.Id == userIdToRemove).ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancel);
            string removerName = usersInfo.GetValueOrDefault(currentUserId, "Một thành viên")!;
            string removedName = usersInfo.GetValueOrDefault(userIdToRemove, "người dùng")!;

            bool isLeaving = currentUserId == userIdToRemove;
            string systemContent;
            SystemMessageMetadata systemMetadata;
            var actorSnapshot = new SnapshotUser { Id = currentUserId, Name = removerName };

            if (isLeaving)
            {
                systemContent = $"{removerName} đã rời nhóm.";
                _context.ConversationParticipants.Remove(targetParticipant);
                systemMetadata = new MemberLeftMetadata { Actor = actorSnapshot };
            }
            else
            {
                string ownerRole = ConversationRole.Owner.GetDescription();
                string modRole = ConversationRole.Moderator.GetDescription();

                if (currentUserParticipant.Role != modRole && currentUserParticipant.Role != ownerRole)
                {
                    _logger.LogWarning("User {UserId} attempted to remove member {TargetUserId} from conversation {ConversationId} without moderator permissions", currentUserId, userIdToRemove, conversationId);
                    throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You don't have permission to remove members.");
                }

                systemContent = $"{removerName} đã xóa {removedName} khỏi nhóm.";
                targetParticipant.IsKicked = true;
                _context.ConversationParticipants.Update(targetParticipant);
                systemMetadata = new MemberRemovedMetadata
                {
                    Actor = actorSnapshot,
                    Targets = new List<SnapshotUser> { new SnapshotUser { Id = userIdToRemove, Name = removedName } }
                };
            }

            var now = DateTime.UtcNow;
            var systemMessage = new Message
            {
                Id = Guid.NewGuid().ToString(),
                ConversationId = conversationId,
                SenderId = currentUserId,
                Type = MessageType.System.GetDescription(),
                Content = systemContent,
                Metadata = JsonSerializer.Serialize(systemMetadata, _jsonCamelCase),
                CreatedAt = now
            };

            currentUserParticipant.LastReadMessageId = systemMessage.Id;
            currentUserParticipant.LastReadAt = now;
            conversation.LastMessageAt = now;

            _context.ConversationParticipants.Update(currentUserParticipant);
            _context.Conversations.Update(conversation);
            _context.Messages.Add(systemMessage);

            await _context.SaveChangesAsync(cancel);

            var activeIds = await _context.ConversationParticipants.AsNoTracking().Where(p => p.ConversationId == conversationId && !p.IsKicked).Select(p => p.UserId).ToListAsync(cancel);
            var msgResponse = await MapToMessageResponseAsync(systemMessage.Id, currentUserId, cancel);

            try
            {
                await _chatHubContext.Clients.User(userIdToRemove).SendAsync("RemovedFromConversation", conversationId, cancel);
                await _chatHubContext.Clients.Users(activeIds).SendAsync("ReceiveNewMessage", msgResponse, cancel);
            }
            catch (Exception ex) { _logger.LogError(ex, "SignalR Error RemoveMemberFromGroupAsync"); }

            _logger.LogInformation("User {UserId} removed user {TargetUserId} from conversation {ConversationId}. IsLeaving: {IsLeaving}", currentUserId, userIdToRemove, conversationId, isLeaving);
            return true;
        }

        /// <inheritdoc />
        public async Task<PaginatedData<ParticipantResponse>> GetGroupMembersAsync(string currentUserId, string conversationId, CursorPaginationRequest request, CancellationToken cancel)
        {
            _logger.LogInformation("User {UserId} is retrieving members for conversation {ConversationId}", currentUserId, conversationId);

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                _logger.LogWarning("Get group members rejected for conversation {ConversationId} because current user ID is empty", conversationId);
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");
            }

            if (string.IsNullOrWhiteSpace(conversationId))
            {
                _logger.LogWarning("Get group members rejected for user {UserId} because conversation ID is empty", currentUserId);
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Conversation ID is required.");
            }

            var limit = request.Limit <= 0 ? 20 : Math.Min(request.Limit, 50);

            var isMember = await _context.ConversationParticipants
                .AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == currentUserId && !cp.IsKicked, cancel);

            if (!isMember)
            {
                _logger.LogWarning("User {UserId} attempted to retrieve members for conversation {ConversationId} without active membership", currentUserId, conversationId);
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You are not an active member of this group.");
            }

            string? cursorId = null;
            if (!string.IsNullOrWhiteSpace(request.Cursor))
            {
                var decoded = CursorHelper.Decode<GroupMemberCursorPayload>(request.Cursor);
                if (decoded != null)
                {
                    cursorId = decoded.ParticipantId;
                }
            }

            var baseQuery = _context.ConversationParticipants.AsNoTracking()
                .Where(p => p.ConversationId == conversationId && !p.IsKicked);

            if (!string.IsNullOrWhiteSpace(cursorId))
            {
                baseQuery = baseQuery.Where(p => string.Compare(p.Id, cursorId) > 0);
            }

            var rawParticipants = await baseQuery
                .OrderBy(p => p.Id)
                .Take(limit + 1)
                .Select(p => new { p.Id, p.UserId, p.Role, p.LastReadMessageId })
                .ToListAsync(cancel);

            string? nextCursor = null;
            if (rawParticipants.Count > limit)
            {
                var lastItem = rawParticipants[limit - 1];
                nextCursor = CursorHelper.Encode(new GroupMemberCursorPayload
                {
                    ParticipantId = lastItem.Id
                });
                rawParticipants.RemoveAt(limit);
            }

            var userIdsToFetch = rawParticipants.Select(p => p.UserId).ToList();

            var usersDict = userIdsToFetch.Any()
                ? await BaseUserProjectionQuery()
                    .Where(u => userIdsToFetch.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u, cancel)
                : new Dictionary<string, UserResponse>();

            var isFriendDictionary = await GetFriendshipStatusesAsync(currentUserId, userIdsToFetch);
            var onlineStatuses = await _redisService.GetUsersOnlineStatusAsync(userIdsToFetch);

            var items = rawParticipants
                .Where(p => usersDict.ContainsKey(p.UserId))
                .Select(p =>
                {
                    bool isFriend = isFriendDictionary.GetValueOrDefault(p.UserId);
                    bool isOnline = isFriend && onlineStatuses.GetValueOrDefault(p.UserId);

                    return MapParticipant(
                        usersDict[p.UserId],
                        p.Role,
                        p.LastReadMessageId,
                        isOnline,
                        isFriend
                    );
                })
                .ToList();

            _logger.LogInformation("User {UserId} retrieved {Count} members for conversation {ConversationId}", currentUserId, items.Count, conversationId);
            return new PaginatedData<ParticipantResponse>
            {
                Items = items,
                Pagination = new CursorPaginationMeta
                {
                    NextCursor = nextCursor,
                    Limit = limit
                }
            };
        }

        private IQueryable<UserResponse> BaseUserProjectionQuery()
        {
            return _context.Users.AsNoTracking().Select(u => new UserResponse
            {
                Id = u.Id,
                DisplayName = u.DisplayName ?? u.Username,
                Username = u.Username,
                AvatarUrl = _context.UserMedias
                    .Where(um => um.UserId == u.Id && um.IsPrimary && um.MediaType == "Avatar")
                    .Select(um => um.MediaUrl)
                    .FirstOrDefault()
            });
        }

        private ParticipantResponse MapParticipant(UserResponse u, string role, string? lastReadMsgId, bool isOnline, bool isFriend)
        {
            return new ParticipantResponse
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
                Username = u.Username,
                AvatarUrl = u.AvatarUrl,
                Role = role,
                LastReadMessageId = lastReadMsgId,
                IsOnline = isOnline,
                IsFriend = isFriend
            };
        }

        private async Task<Dictionary<string, bool>> GetFriendshipStatusesAsync(string currentUserId, IEnumerable<string> userIdsToFetch)
        {
            var distinctUserIds = userIdsToFetch.Distinct().ToList();

            if (!distinctUserIds.Any())
            {
                return new Dictionary<string, bool>();
            }

            var actualFriendIds = await _context.Friendships.AsNoTracking()
                .Where(fs =>
                    (fs.UserLowId == currentUserId && distinctUserIds.Contains(fs.UserHighId)) ||
                    (fs.UserHighId == currentUserId && distinctUserIds.Contains(fs.UserLowId))
                )
                .Select(fs => fs.UserLowId == currentUserId ? fs.UserHighId : fs.UserLowId)
                .ToListAsync();

            var friendIdsSet = actualFriendIds.ToHashSet();

            return distinctUserIds.ToDictionary(
                userId => userId,
                userId => friendIdsSet.Contains(userId)
            );
        }

        private async Task<MessageResponse> MapToMessageResponseAsync(string messageId, string currentUserId, CancellationToken cancel)
        {
            var messageData = await _context.Messages.AsNoTracking().Where(m => m.Id == messageId)
                .Select(m => new
                {
                    m.Id,
                    m.ConversationId,
                    m.ClientMessageId,
                    m.SenderId,
                    m.Type,
                    m.Content,
                    m.CreatedAt,
                    m.UpdatedAt,
                    m.IsDeleted,
                    m.Metadata,
                    m.ReplyToMessageId,
                    SenderName = _context.Users.Where(u => u.Id == m.SenderId).Select(u => u.DisplayName ?? u.Username).FirstOrDefault(),
                    SenderAvatarUrl = _context.UserMedias.Where(um => um.UserId == m.SenderId && um.IsPrimary && um.MediaType == "Avatar").Select(um => um.MediaUrl).FirstOrDefault()
                }).FirstOrDefaultAsync(cancel);

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            SystemMessageMetadata? ParseSystemMetadata(string type, string? metadata)
            {
                if (type == "System" && !string.IsNullOrWhiteSpace(metadata))
                {
                    try
                    {
                        return JsonSerializer.Deserialize<SystemMessageMetadata>(metadata, jsonOptions);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error when deserialize message metadata");
                    }
                }
                return null;
            }

            return new MessageResponse
            {
                Id = messageData.Id,
                ConversationId = messageData.ConversationId,
                ClientMessageId = messageData.ClientMessageId,
                SenderId = messageData.SenderId,
                SenderName = messageData.SenderName ?? "Unknown User",
                SenderAvatarUrl = messageData.SenderAvatarUrl,
                Type = messageData.Type,
                Content = messageData.IsDeleted ? null : messageData.Content,
                CreatedAt = messageData.CreatedAt.ToUtc(),
                UpdatedAt = messageData.UpdatedAt?.ToUtc(),
                IsDeleted = messageData.IsDeleted,
                ReplyToMessageId = messageData.ReplyToMessageId,
                ActionMetadata = ParseSystemMetadata(messageData.Type, messageData.Metadata)
            };
        }
    }
}
