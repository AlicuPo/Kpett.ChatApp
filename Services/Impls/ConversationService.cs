using Kpett.ChatApp.Constants;
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
using Kpett.ChatApp.Extensions;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Hubs;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Kpett.ChatApp.Services.Impls
{
    /// <summary>Service quản lý hội thoại: CRUD, tin nhắn, thành viên, cài đặt (uỷ quyền message/member cho sub-services).</summary>
    public class ConversationService : IConversationService
    {
        private readonly AppDbContext _context;
        private readonly IRedisService _redisService;
        private readonly IHubContext<AppHub> _chatHubContext;
        private readonly ILogger<ConversationService> _logger;
        private readonly IConversationMessageService _conversationMessageService;
        private readonly IConversationMemberService _conversationMemberService;

        private static readonly JsonSerializerOptions _jsonCamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private static readonly JsonSerializerOptions _jsonCaseInsensitive = new() { PropertyNameCaseInsensitive = true };

        /// <summary>Khởi tạo service với các dependencies.</summary>
        public ConversationService(
            AppDbContext dbContext,
            IRedisService redisService,
            IHubContext<AppHub> chatHubContext,
            ILogger<ConversationService> logger,
            IConversationMessageService conversationMessageService,
            IConversationMemberService conversationMemberService)
        {
            _context = dbContext;
            _redisService = redisService;
            _chatHubContext = chatHubContext;
            _logger = logger;
            _conversationMessageService = conversationMessageService;
            _conversationMemberService = conversationMemberService;
        }

        /// <inheritdoc />
        public async Task<ConversationResponse> CreateConversationAsync(string currentUserId, CreateConversationRequest request, CancellationToken cancel)
        {
            _logger.LogInformation("User {UserId} is creating conversation. Type: {ConversationType}. ParticipantCount: {ParticipantCount}", currentUserId, request?.Type, request?.ParticipantIds?.Count ?? 0);

            // 1. VALIDATE ĐẦU VÀO CƠ BẢN
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                _logger.LogWarning("Create conversation rejected because current user ID is empty");
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");
            }

            if (request == null || string.IsNullOrWhiteSpace(request.Type))
            {
                _logger.LogWarning("Create conversation rejected for user {UserId} because request or type is empty", currentUserId);
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Conversation request and type are required.");
            }

            var isGroup = request.Type.Equals("Group", StringComparison.OrdinalIgnoreCase);
            var isDirect = request.Type.Equals("Direct", StringComparison.OrdinalIgnoreCase);

            if (!isGroup && !isDirect)
            {
                _logger.LogWarning("Create conversation rejected for user {UserId} because type {ConversationType} is invalid", currentUserId, request.Type);
                throw new BadRequestException(ErrorCodes.VALIDATION.INVALID, "Invalid conversation type. Must be 'Direct' or 'Group'.");
            }

            var uniqueParticipantIds = new HashSet<string>(request.ParticipantIds.Where(id => !string.IsNullOrWhiteSpace(id))) { currentUserId };

            // 2. VALIDATE LOGIC THEO TỪNG LOẠI
            if (isDirect)
            {
                if (uniqueParticipantIds.Count != 2)
                {
                    _logger.LogWarning("Create direct conversation rejected for user {UserId} because participant count is {ParticipantCount}", currentUserId, uniqueParticipantIds.Count);
                    throw new BadRequestException(ErrorCodes.VALIDATION.INVALID, "Direct conversation must have exactly 2 different participants.");
                }
                if (string.IsNullOrWhiteSpace(request.InitialMessage))
                {
                    _logger.LogWarning("Create direct conversation rejected for user {UserId} because initial message is empty", currentUserId);
                    throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Initial message content is required to create a direct conversation.");
                }
            }
            else if (isGroup)
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    _logger.LogWarning("Create group conversation rejected for user {UserId} because group name is empty", currentUserId);
                    throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Group name is required.");
                }
                if (uniqueParticipantIds.Count < 2)
                {
                    _logger.LogWarning("Create group conversation rejected for user {UserId} because participant count is {ParticipantCount}", currentUserId, uniqueParticipantIds.Count);
                    throw new BadRequestException(ErrorCodes.VALIDATION.INVALID, "A group must have at least 2 participants.");
                }
            }

            var now = DateTime.UtcNow;

            // 3. TRUY VẤN USERS (Tái sử dụng hàm helper)
            var usersInfo = await BaseUserProjectionQuery()
                .Where(u => uniqueParticipantIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u, cancel);

            if (usersInfo.Count != uniqueParticipantIds.Count)
            {
                _logger.LogWarning("Create conversation rejected for user {UserId} because one or more participants were not found", currentUserId);
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "One or more participants do not exist.");
            }

            // 4. XỬ LÝ RIÊNG CHO DIRECT CHAT (Tái sử dụng phòng cũ)
            if (isDirect)
            {
                var participantsArray = uniqueParticipantIds.ToArray();
                bool isOrderCorrect = string.CompareOrdinal(participantsArray[0], participantsArray[1]) < 0;
                var userLow = isOrderCorrect ? participantsArray[0] : participantsArray[1];
                var userHigh = isOrderCorrect ? participantsArray[1] : participantsArray[0];

                var existingKey = await _context.ConversationKeys
                    .FirstOrDefaultAsync(k => k.UserLowId == userLow && k.UserHighId == userHigh && k.ConversationId != null, cancel);

                if (existingKey != null)
                {
                    var existingConversation = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == existingKey.ConversationId, cancel);
                    if (existingConversation != null)
                    {
                        var sendRequest = new SendMessageRequest
                        {
                            ClientMessageId = Guid.NewGuid().ToString(),
                            Content = request.InitialMessage,
                            Type = MessageType.Text.GetDescription()
                        };
                        await SendMessageAsync(currentUserId, existingConversation.Id, sendRequest, cancel);

                        _logger.LogInformation("Direct conversation already existed for user {UserId}. Reused conversation {ConversationId}", currentUserId, existingConversation.Id);
                        return await GetConversationByIdAsync(currentUserId, existingConversation.Id, cancel);
                    }
                }
            }

            // 5. KHỞI TẠO ENTITIES CHO PHÒNG MỚI
            var conversationId = Guid.NewGuid().ToString();
            var newConversation = new Conversation
            {
                Id = conversationId,
                Type = isGroup ? "Group" : "Direct",
                Name = isGroup ? request.Name : null,
                LastMessageAt = now,
                CreatedAt = now,
                IsActive = true
            };

            string? metadataJson = null;
            var isSystemMessage = isGroup && string.IsNullOrWhiteSpace(request.InitialMessage);
            var messageType = isSystemMessage ? MessageType.System.GetDescription() : MessageType.Text.GetDescription();
            var messageContent = isSystemMessage ? "Đã tạo nhóm" : request.InitialMessage;

            if (isSystemMessage)
            {
                var systemMetadata = new GroupCreatedMetadata
                {
                    Actor = new SnapshotUser { Id = currentUserId, Name = usersInfo[currentUserId].DisplayName },
                    Targets = uniqueParticipantIds.Where(id => id != currentUserId)
                                .Select(id => new SnapshotUser { Id = id, Name = usersInfo[id].DisplayName }).ToList()
                };
                metadataJson = JsonSerializer.Serialize(systemMetadata, _jsonCamelCase);
            }

            var initialMessage = new Message
            {
                Id = Guid.NewGuid().ToString(),
                ConversationId = conversationId,
                SenderId = currentUserId,
                Content = messageContent,
                Type = messageType,
                Metadata = metadataJson,
                CreatedAt = now,
            };

            var ownerRole = ConversationRole.Owner.GetDescription();
            var memberRole = ConversationRole.Member.GetDescription();

            var participants = uniqueParticipantIds.Select(userId => new ConversationParticipant
            {
                Id = Guid.NewGuid().ToString(),
                ConversationId = conversationId,
                UserId = userId,
                JoinedAt = now,
                LastReadAt = userId == currentUserId ? now : DateTime.MinValue,
                LastReadMessageId = userId == currentUserId ? initialMessage.Id : null,
                Role = (isGroup && userId == currentUserId) ? ownerRole : memberRole,
            }).ToList();

            // 6. LƯU VÀO DATABASE
            _context.Conversations.Add(newConversation);
            _context.Messages.Add(initialMessage);
            _context.ConversationParticipants.AddRange(participants);

            if (isDirect)
            {
                var participantsArray = uniqueParticipantIds.ToArray();
                bool isOrderCorrect = string.CompareOrdinal(participantsArray[0], participantsArray[1]) < 0;
                _context.ConversationKeys.Add(new ConversationKey
                {
                    Id = Guid.NewGuid().ToString(),
                    ConversationId = conversationId,
                    UserLowId = isOrderCorrect ? participantsArray[0] : participantsArray[1],
                    UserHighId = isOrderCorrect ? participantsArray[1] : participantsArray[0]
                });
            }

            try
            {
                await _context.SaveChangesAsync(cancel);
            }
            catch (DbUpdateException) when (isDirect)
            {
                _logger.LogWarning("Direct conversation creation hit duplicate key for user {UserId}. Resolving existing conversation", currentUserId);
                foreach (var entry in _context.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged))
                {
                    entry.State = EntityState.Detached;
                }

                var participantsArray = uniqueParticipantIds.ToArray();
                bool isOrderCorrect = string.CompareOrdinal(participantsArray[0], participantsArray[1]) < 0;
                var userLow = isOrderCorrect ? participantsArray[0] : participantsArray[1];
                var userHigh = isOrderCorrect ? participantsArray[1] : participantsArray[0];

                var existingConversationId = await _context.ConversationKeys.AsNoTracking()
                    .Where(k => k.UserLowId == userLow && k.UserHighId == userHigh && k.ConversationId != null)
                    .Select(k => k.ConversationId)
                    .FirstOrDefaultAsync(cancel);

                if (string.IsNullOrWhiteSpace(existingConversationId))
                {
                    _logger.LogWarning("Direct conversation duplicate resolution failed for user {UserId}", currentUserId);
                    throw;
                }

                await SendMessageAsync(currentUserId, existingConversationId, new SendMessageRequest
                {
                    ClientMessageId = Guid.NewGuid().ToString(),
                    Content = request.InitialMessage,
                    Type = MessageType.Text.GetDescription()
                }, cancel);

                _logger.LogInformation("Direct conversation duplicate resolved for user {UserId}. Reused conversation {ConversationId}", currentUserId, existingConversationId);
                return await GetConversationByIdAsync(currentUserId, existingConversationId, cancel);
            }

            // 7. MAP RESPONSE
            SystemMessageMetadata? actionMetadata = ParseSystemMetadata(initialMessage.Type, initialMessage.Metadata);
            var onlineStatuses = await _redisService.GetUsersOnlineStatusAsync(uniqueParticipantIds);
            var isFriendDictionary = await GetFriendshipStatusesAsync(currentUserId, uniqueParticipantIds);

            var newConversationResponse = new ConversationResponse
            {
                Id = newConversation.Id,
                Type = newConversation.Type,
                Name = newConversation.Name,
                AvatarUrl = newConversation.AvatarUrl,
                CreatedAt = newConversation.CreatedAt.ToUtc(),
                LastMessageAt = newConversation.LastMessageAt.ToUtc(),
                IsActive = newConversation.IsActive,
                HasUnread = true,
                LastMessage = new MessageSnippetResponse
                {
                    Id = initialMessage.Id,
                    SenderId = initialMessage.SenderId,
                    SenderName = usersInfo[currentUserId].DisplayName,
                    Content = initialMessage.Content,
                    Type = initialMessage.Type,
                    ActionMetadata = actionMetadata,
                    CreatedAt = initialMessage.CreatedAt
                },
                Participants = participants.Select(p =>
                {
                    bool isFriend = isFriendDictionary.GetValueOrDefault(p.UserId);
                    bool isOnline = isFriend && onlineStatuses.GetValueOrDefault(p.UserId);

                    return MapParticipant(
                         usersInfo[p.UserId],
                         p.Role,
                         p.LastReadMessageId,
                         isOnline,
                         isFriend
                     );
                })
                .ToList()
            };

            // [SIGNALR PUSH] Bắn bất đồng bộ
            try
            {
                foreach (var userId in uniqueParticipantIds)
                {
                    await _chatHubContext.Clients.User(userId).SendAsync("NewConversationCreated", newConversationResponse, cancel);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pushing NewConversationCreated");
            }

            _logger.LogInformation("User {UserId} created conversation {ConversationId}. Type: {ConversationType}. ParticipantCount: {ParticipantCount}", currentUserId, newConversation.Id, newConversation.Type, participants.Count);
            return newConversationResponse;
        }

        /// <inheritdoc />
        public async Task<PaginatedData<ConversationResponse>> GetConversationsAsync(string currentUserId, ConversationListRequest request, CancellationToken cancel)
        {
            _logger.LogInformation("User {UserId} is retrieving conversations", currentUserId);

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                _logger.LogWarning("Get conversations rejected because current user ID is empty");
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");
            }

            var limit = request.Limit <= 0 ? 12 : Math.Min(request.Limit, 20);
            var searchTerm = request.Search?.Trim();
            DateTime? cursorDate = null;
            string? cursorId = null;

            if (!string.IsNullOrWhiteSpace(request.Cursor))
            {
                var decoded = CursorHelper.Decode<ConversationCursorPayload>(request.Cursor);
                if (decoded != null)
                {
                    cursorDate = decoded.LastMessageAt;
                    cursorId = decoded.ConversationId;
                }
            }

            var baseQuery = _context.ConversationParticipants.AsNoTracking()
                .Where(cp => cp.UserId == currentUserId && !cp.IsKicked)
                .Join(_context.Conversations.AsNoTracking().Where(c => c.IsActive == true),
                    cp => cp.ConversationId, c => c.Id,
                    (cp, c) => new { Participant = cp, Conversation = c });

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                baseQuery = baseQuery.Where(x =>
                    (x.Conversation.Type == "Group" && x.Conversation.Name != null && x.Conversation.Name.Contains(searchTerm)) ||
                    (x.Conversation.Type == "Direct" && _context.ConversationParticipants.Any(p =>
                        p.ConversationId == x.Conversation.Id && p.UserId != currentUserId &&
                        _context.Users.Any(u => u.Id == p.UserId && ((u.DisplayName != null && u.DisplayName.Contains(searchTerm)) || (u.Username != null && u.Username.Contains(searchTerm))))
                    ))
                );
            }

            if (cursorDate.HasValue && !string.IsNullOrWhiteSpace(cursorId))
            {
                baseQuery = baseQuery.Where(x => x.Conversation.LastMessageAt < cursorDate.Value || (x.Conversation.LastMessageAt == cursorDate.Value && string.Compare(x.Conversation.Id, cursorId) < 0));
            }

            // Lấy thêm 1 record để check next page
            var rawConversations = await baseQuery.Select(x => new
            {
                x.Conversation.Id,
                x.Conversation.Type,
                x.Conversation.Name,
                x.Conversation.AvatarUrl,
                x.Conversation.CreatedAt,
                x.Conversation.LastMessageAt,
                x.Conversation.IsActive,
                x.Participant.LastReadAt,
                LastMessage = _context.Messages.Where(m => m.ConversationId == x.Conversation.Id).OrderByDescending(m => m.CreatedAt).FirstOrDefault(),
                OtherParticipant = x.Conversation.Type == "Direct" ? _context.ConversationParticipants.Where(p => p.ConversationId == x.Conversation.Id && p.UserId != currentUserId).FirstOrDefault() : null
            })
            .OrderByDescending(x => x.LastMessageAt).ThenByDescending(x => x.Id).Take(limit + 1).ToListAsync(cancel);

            string? nextCursor = null;
            if (rawConversations.Count > limit)
            {
                var lastItem = rawConversations[limit - 1];
                nextCursor = CursorHelper.Encode(new ConversationCursorPayload { ConversationId = lastItem.Id, LastMessageAt = lastItem.LastMessageAt });
                rawConversations.RemoveAt(limit);
            }

            var userIdsToFetch = new HashSet<string>();
            var groupMembersDict = new Dictionary<string, List<ConversationParticipant>>();

            foreach (var c in rawConversations)
            {
                if (c.Type == "Direct" && c.OtherParticipant != null) userIdsToFetch.Add(c.OtherParticipant.UserId);
                if (c.LastMessage != null) userIdsToFetch.Add(c.LastMessage.SenderId);
            }

            var groupIds = rawConversations.Where(c => c.Type == "Group").Select(c => c.Id).ToList();
            if (groupIds.Any())
            {
                var groupParticipants = await _context.ConversationParticipants.AsNoTracking()
                    .Where(p => groupIds.Contains(p.ConversationId) && !p.IsKicked)
                    .ToListAsync(cancel);

                foreach (var gp in groupParticipants.GroupBy(p => p.ConversationId))
                {
                    var previewMembers = gp.Take(3).ToList();
                    groupMembersDict[gp.Key] = previewMembers;
                    foreach (var p in previewMembers) userIdsToFetch.Add(p.UserId);
                }
            }

            var usersDict = userIdsToFetch.Any()
                ? await BaseUserProjectionQuery().Where(u => userIdsToFetch.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u, cancel)
                : new Dictionary<string, UserResponse>();

            var onlineStatuses = await _redisService.GetUsersOnlineStatusAsync(userIdsToFetch);

            var isFriendDictionary = await GetFriendshipStatusesAsync(currentUserId, userIdsToFetch);

            var items = rawConversations.Select(c =>
            {
                bool isDirect = c.Type == "Direct";
                var participants = new List<ParticipantResponse>();
                string? displayName = c.Name;
                string? avatarUrl = c.AvatarUrl;

                if (isDirect && c.OtherParticipant != null && usersDict.TryGetValue(c.OtherParticipant.UserId, out var otherUser))
                {
                    displayName = otherUser.DisplayName;
                    avatarUrl = otherUser.AvatarUrl;

                    bool isFriend = isFriendDictionary.GetValueOrDefault(otherUser.Id);
                    bool isOnline = isFriend && onlineStatuses.GetValueOrDefault(otherUser.Id);

                    participants.Add(MapParticipant(
                            otherUser,
                            c.OtherParticipant.Role,
                            c.OtherParticipant.LastReadMessageId,
                            isOnline,
                            isFriend
                        ));
                }
                else if (!isDirect && groupMembersDict.TryGetValue(c.Id, out var members))
                {
                    participants.AddRange(members.Where(m => usersDict.ContainsKey(m.UserId))
                        .Select(m =>
                        {
                            bool isFriend = isFriendDictionary.GetValueOrDefault(m.UserId);

                            bool isOnline = isFriend && onlineStatuses.GetValueOrDefault(m.UserId);

                            return MapParticipant(usersDict[m.UserId], m.Role, m.LastReadMessageId, isOnline, isFriend);
                        }));
                }


                MessageSnippetResponse? lastMessageResponse = null;
                if (c.LastMessage != null)
                {
                    lastMessageResponse = new MessageSnippetResponse
                    {
                        Id = c.LastMessage.Id,
                        SenderId = c.LastMessage.SenderId,
                        SenderName = usersDict.GetValueOrDefault(c.LastMessage.SenderId)?.DisplayName ?? "Unknown User",
                        Type = c.LastMessage.Type,
                        Content = c.LastMessage.Content,
                        CreatedAt = c.LastMessage.CreatedAt,
                        ActionMetadata = ParseSystemMetadata(c.LastMessage.Type, c.LastMessage.Metadata)
                    };
                }

                return new ConversationResponse
                {
                    Id = c.Id,
                    Type = c.Type,
                    Name = displayName,
                    AvatarUrl = avatarUrl,
                    CreatedAt = c.CreatedAt.ToUtc(),
                    LastMessageAt = c.LastMessageAt.ToUtc(),
                    IsActive = c.IsActive,
                    HasUnread = !c.LastReadAt.HasValue || c.LastReadAt.Value == DateTime.MinValue || c.LastMessageAt > c.LastReadAt.Value,
                    LastMessage = lastMessageResponse,
                    Participants = participants
                };
            }).ToList();

            _logger.LogInformation("User {UserId} retrieved {Count} conversations", currentUserId, items.Count);
            return new PaginatedData<ConversationResponse>
            {
                Items = items,
                Pagination = new CursorPaginationMeta
                {
                    NextCursor = nextCursor,
                    Limit = limit
                }
            };
        }

        /// <inheritdoc />
        public async Task<bool> HasUnreadConversationAsync(string currentUserId, CancellationToken cancel)
        {
            _logger.LogDebug("Checking unread conversations for user {UserId}", currentUserId);

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                _logger.LogWarning("Unread conversation check rejected because current user ID is empty");
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");
            }

            var hasUnread = await _context.ConversationParticipants.AsNoTracking()
                .Where(cp => cp.UserId == currentUserId && !cp.IsKicked)
                .Join(
                    _context.Conversations.AsNoTracking().Where(c => c.IsActive),
                    cp => cp.ConversationId,
                    c => c.Id,
                    (cp, c) => new { Participant = cp, Conversation = c })
                .AnyAsync(x =>
                    !x.Participant.LastReadAt.HasValue ||
                    x.Participant.LastReadAt.Value == DateTime.MinValue ||
                    x.Conversation.LastMessageAt > x.Participant.LastReadAt.Value,
                    cancel);
            _logger.LogDebug("Unread conversation check completed for user {UserId}. HasUnread: {HasUnread}", currentUserId, hasUnread);
            return hasUnread;
        }

        /// <inheritdoc />
        public Task<bool> AddMembersToGroupAsync(string currentUserId, AddMembersRequest request, CancellationToken cancel)
        {
            return _conversationMemberService.AddMembersToGroupAsync(currentUserId, request, cancel);
        }

        /// <inheritdoc />
        public Task<bool> RemoveMemberFromGroupAsync(string currentUserId, string conversationId, string userIdToRemove, CancellationToken cancel)
        {
            return _conversationMemberService.RemoveMemberFromGroupAsync(currentUserId, conversationId, userIdToRemove, cancel);
        }

        /// <inheritdoc />
        public Task<PaginatedData<MessageResponse>> GetMessagesAsync(string currentUserId, string conversationId, MessageListRequest request, CancellationToken cancel)
        {
            return _conversationMessageService.GetMessagesAsync(currentUserId, conversationId, request, cancel);
        }

        /// <inheritdoc />
        public Task<MessageResponse> SendMessageAsync(string currentUserId, string conversationId, SendMessageRequest request, CancellationToken cancel)
        {
            return _conversationMessageService.SendMessageAsync(currentUserId, conversationId, request, cancel);
        }

        /// <inheritdoc />
        public Task<MessageResponse> UpdateMessageAsync(string currentUserId, string conversationId, string messageId, UpdateMessageRequest request, CancellationToken cancel)
        {
            return _conversationMessageService.UpdateMessageAsync(currentUserId, conversationId, messageId, request, cancel);
        }

        /// <inheritdoc />
        public Task DeleteMessageAsync(string currentUserId, string conversationId, string messageId, CancellationToken cancel)
        {
            return _conversationMessageService.DeleteMessageAsync(currentUserId, conversationId, messageId, cancel);
        }

        /// <inheritdoc />
        public async Task MarkAsReadAsync(string conversationId, string currentUserId, CancellationToken cancel)
        {
            _logger.LogDebug("User {UserId} is marking conversation {ConversationId} as read", currentUserId, conversationId);

            if (string.IsNullOrWhiteSpace(currentUserId) || string.IsNullOrWhiteSpace(conversationId))
            {
                _logger.LogDebug("Mark as read skipped because user ID or conversation ID is empty");
                return;
            }

            var latestMessageId = await _context.Messages.AsNoTracking()
                .Where(m => m.ConversationId == conversationId).OrderByDescending(m => m.CreatedAt).Select(m => m.Id).FirstOrDefaultAsync(cancel);

            if (latestMessageId == null)
            {
                _logger.LogDebug("Mark as read skipped because conversation {ConversationId} has no messages", conversationId);
                return;
            }

            var participant = await _context.ConversationParticipants
                .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == currentUserId && !p.IsKicked, cancel);

            if (participant == null)
            {
                _logger.LogDebug("Mark as read skipped because user {UserId} is not an active participant in conversation {ConversationId}", currentUserId, conversationId);
                return;
            }

            participant.LastReadAt = DateTime.UtcNow;
            participant.LastReadMessageId = latestMessageId;

            _context.ConversationParticipants.Update(participant);
            await _context.SaveChangesAsync(cancel);
            _logger.LogInformation("User {UserId} marked conversation {ConversationId} as read up to message {MessageId}", currentUserId, conversationId, latestMessageId);

            var otherUserIds = await _context.ConversationParticipants.AsNoTracking()
                .Where(p => p.ConversationId == conversationId && p.UserId != currentUserId && !p.IsKicked).Select(p => p.UserId).ToListAsync(cancel);

            if (!otherUserIds.Any())
            {
                return;
            }

            try
            {
                await _chatHubContext.Clients.Users(otherUserIds).SendAsync("UserReadMessage", conversationId, currentUserId, latestMessageId, cancel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when send UserReadMessage");
            }
        }

        /// <inheritdoc />
        public async Task<ConversationViewerContextResponse> UpdateConversationSettingsAsync(string currentUserId, string conversationId, UpdateConversationSettingsRequest request, CancellationToken cancel)
        {
            _logger.LogInformation("User {UserId} is updating settings for conversation {ConversationId}", currentUserId, conversationId);

            if (string.IsNullOrWhiteSpace(currentUserId) || string.IsNullOrWhiteSpace(conversationId))
            {
                _logger.LogWarning("Update conversation settings rejected because user ID or conversation ID is empty");
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Conversation ID and user ID are required.");
            }

            if (request == null || (!request.IsMuted.HasValue && !request.IsArchived.HasValue))
            {
                _logger.LogWarning("Update conversation settings rejected for user {UserId} in conversation {ConversationId} because no settings were provided", currentUserId, conversationId);
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "At least one setting value is required.");
            }

            var participant = await _context.ConversationParticipants
                .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == currentUserId && !p.IsKicked, cancel);

            if (participant == null)
            {
                _logger.LogWarning("User {UserId} attempted to update settings for conversation {ConversationId} without active membership", currentUserId, conversationId);
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You are not an active member of this conversation.");
            }

            if (request.IsMuted.HasValue)
            {
                participant.IsMuted = request.IsMuted.Value;
            }

            if (request.IsArchived.HasValue)
            {
                participant.IsArchived = request.IsArchived.Value;
            }

            await _context.SaveChangesAsync(cancel);
            _logger.LogInformation("User {UserId} updated settings for conversation {ConversationId}. IsMuted: {IsMuted}. IsArchived: {IsArchived}", currentUserId, conversationId, participant.IsMuted, participant.IsArchived);

            return new ConversationViewerContextResponse
            {
                IsMuted = participant.IsMuted,
                IsArchived = participant.IsArchived,
                LastReadMessageId = participant.LastReadMessageId,
                Permissions = new ConversationPermissionsResponse
                {
                    CanSendMessage = true,
                    CanAddParticipants = participant.Role == ConversationRole.Owner.GetDescription() ||
                                         participant.Role == ConversationRole.Moderator.GetDescription(),
                    CanRemoveParticipants = participant.Role == ConversationRole.Owner.GetDescription() ||
                                            participant.Role == ConversationRole.Moderator.GetDescription(),
                    CanChangeName = participant.Role == ConversationRole.Owner.GetDescription() ||
                                    participant.Role == ConversationRole.Moderator.GetDescription(),
                    CanModerateMessages = participant.Role == ConversationRole.Owner.GetDescription() ||
                                          participant.Role == ConversationRole.Moderator.GetDescription()
                }
            };
        }

        /// <inheritdoc />
        public async Task<ConversationResponse> GetConversationByIdAsync(string currentUserId, string conversationId, CancellationToken cancel)
        {
            _logger.LogInformation("User {UserId} is retrieving conversation {ConversationId}", currentUserId, conversationId);

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                _logger.LogWarning("Get conversation rejected for conversation {ConversationId} because current user ID is empty", conversationId);
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");
            }
            if (string.IsNullOrWhiteSpace(conversationId))
            {
                _logger.LogWarning("Get conversation rejected for user {UserId} because conversation ID is empty", currentUserId);
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Conversation ID is required.");
            }

            var rawData = await _context.ConversationParticipants.AsNoTracking()
            .Where(cp => cp.ConversationId == conversationId && cp.UserId == currentUserId && !cp.IsKicked)
            .Join(_context.Conversations.AsNoTracking(), cp => cp.ConversationId, c => c.Id, (cp, c) => new { Participant = cp, Conversation = c })
            .Select(x => new
            {
                x.Conversation.Id,
                x.Conversation.Type,
                x.Conversation.Name,
                x.Conversation.AvatarUrl,
                x.Conversation.CreatedAt,
                x.Conversation.LastMessageAt,
                x.Conversation.IsActive,
                x.Participant.LastReadAt,
                LastMessage = _context.Messages.Where(m => m.ConversationId == x.Conversation.Id).OrderByDescending(m => m.CreatedAt).FirstOrDefault(),
                OtherParticipant = x.Conversation.Type == "Direct" ? _context.ConversationParticipants.Where(p => p.ConversationId == x.Conversation.Id && p.UserId != currentUserId).FirstOrDefault() : null
            }).FirstOrDefaultAsync(cancel);

            if (rawData == null)
            {
                _logger.LogWarning("Conversation {ConversationId} was not found or user {UserId} is not an active member", conversationId, currentUserId);
                throw new NotFoundException(ErrorCodes.CONVERSATION.NOT_FOUND, "Conversation not found or you are not an active member.");
            }

            var userIdsToFetch = new HashSet<string>();
            var groupMembers = new List<ConversationParticipant>();

            if (rawData.Type == "Direct" && rawData.OtherParticipant != null) userIdsToFetch.Add(rawData.OtherParticipant.UserId);
            if (rawData.LastMessage != null) userIdsToFetch.Add(rawData.LastMessage.SenderId);
            if (rawData.Type == "Group")
            {
                groupMembers = await _context.ConversationParticipants.AsNoTracking()
                    .Where(p => p.ConversationId == conversationId && !p.IsKicked).Take(3).ToListAsync(cancel);
                foreach (var p in groupMembers) userIdsToFetch.Add(p.UserId);
            }

            var usersDict = userIdsToFetch.Any() ? await BaseUserProjectionQuery().Where(u => userIdsToFetch.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u, cancel) : new Dictionary<string, UserResponse>();
            var onlineStatuses = await _redisService.GetUsersOnlineStatusAsync(userIdsToFetch);

            var participantResponses = new List<ParticipantResponse>();
            string? displayName = rawData.Name;
            string? avatarUrl = rawData.AvatarUrl;

            var isFriendDictionary = await GetFriendshipStatusesAsync(currentUserId, userIdsToFetch);

            if (rawData.Type == "Direct" && rawData.OtherParticipant != null && usersDict.TryGetValue(rawData.OtherParticipant.UserId, out var otherUser))
            {
                displayName = otherUser.DisplayName;
                avatarUrl = otherUser.AvatarUrl;

                bool isFriend = isFriendDictionary.GetValueOrDefault(otherUser.Id);
                bool isOnline = isFriend && onlineStatuses.GetValueOrDefault(otherUser.Id);

                participantResponses.Add(MapParticipant(
                    otherUser,
                    rawData.OtherParticipant.Role,
                    rawData.OtherParticipant.LastReadMessageId,
                    isOnline,
                    isFriend
                ));
            }
            else if (rawData.Type == "Group")
            {
                participantResponses.AddRange(groupMembers
                    .Where(m => usersDict.ContainsKey(m.UserId))
                    .Select(m =>
                    {
                        var user = usersDict[m.UserId];

                        bool isFriend = isFriendDictionary.GetValueOrDefault(m.UserId);
                        bool isOnline = isFriend && onlineStatuses.GetValueOrDefault(m.UserId);

                        return MapParticipant(user, m.Role, m.LastReadMessageId, isOnline, isFriend);
                    }));
            }

            MessageSnippetResponse? lastMessageResponse = null;
            if (rawData.LastMessage != null)
            {
                lastMessageResponse = new MessageSnippetResponse
                {
                    Id = rawData.LastMessage.Id,
                    SenderId = rawData.LastMessage.SenderId,
                    SenderName = usersDict.GetValueOrDefault(rawData.LastMessage.SenderId)?.DisplayName ?? "Unknown User",
                    Type = rawData.LastMessage.Type,
                    Content = rawData.LastMessage.Content,
                    CreatedAt = rawData.LastMessage.CreatedAt,
                    ActionMetadata = ParseSystemMetadata(rawData.LastMessage.Type, rawData.LastMessage.Metadata)
                };
            }

            var response = new ConversationResponse
            {
                Id = rawData.Id,
                Type = rawData.Type,
                Name = displayName,
                AvatarUrl = avatarUrl,
                CreatedAt = rawData.CreatedAt.ToUtc(),
                LastMessageAt = rawData.LastMessageAt.ToUtc(),
                IsActive = rawData.IsActive,
                HasUnread = !rawData.LastReadAt.HasValue || rawData.LastReadAt.Value == DateTime.MinValue || rawData.LastMessageAt > rawData.LastReadAt.Value,
                LastMessage = lastMessageResponse,
                Participants = participantResponses
            };
            _logger.LogInformation("User {UserId} retrieved conversation {ConversationId}", currentUserId, conversationId);
            return response;
        }

        /// <inheritdoc />
        public async Task<ConversationResponse> GetOrCreateDirectConversationAsync(string currentUserId, string otherUserId, CancellationToken cancel)
        {
            _logger.LogInformation("User {UserId} is getting or creating direct conversation with user {OtherUserId}", currentUserId, otherUserId);

            if (string.IsNullOrWhiteSpace(currentUserId) || string.IsNullOrWhiteSpace(otherUserId))
            {
                _logger.LogWarning("Get or create direct conversation rejected because one user ID is empty");
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "User IDs are required.");
            }
            if (currentUserId == otherUserId)
            {
                _logger.LogWarning("User {UserId} attempted to create direct conversation with self", currentUserId);
                throw new BadRequestException(ErrorCodes.VALIDATION.INVALID, "Cannot create conversation with yourself.");
            }

            bool isOrderCorrect = string.CompareOrdinal(currentUserId, otherUserId) < 0;
            var userLow = isOrderCorrect ? currentUserId : otherUserId;
            var userHigh = isOrderCorrect ? otherUserId : currentUserId;

            var existingKey = await _context.ConversationKeys.FirstOrDefaultAsync(k => k.UserLowId == userLow && k.UserHighId == userHigh && k.ConversationId != null, cancel);
            if (existingKey != null)
            {
                var existingConversation = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == existingKey.ConversationId, cancel);
                if (existingConversation != null)
                {
                    _logger.LogInformation("Direct conversation already existed between user {UserId} and user {OtherUserId}. ConversationId: {ConversationId}", currentUserId, otherUserId, existingConversation.Id);
                    return await GetConversationByIdAsync(currentUserId, existingConversation.Id, cancel);
                }
            }

            var usersInfo = await BaseUserProjectionQuery().Where(u => u.Id == currentUserId || u.Id == otherUserId).ToDictionaryAsync(u => u.Id, u => u, cancel);
            if (usersInfo.Count != 2)
            {
                _logger.LogWarning("Get or create direct conversation rejected because user {UserId} or user {OtherUserId} was not found", currentUserId, otherUserId);
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "One or both users do not exist.");
            }

            var now = DateTime.UtcNow;
            var conversationId = Guid.NewGuid().ToString();

            var newConversation = new Conversation
            {
                Id = conversationId,
                Type = "Direct",
                LastMessageAt = now,
                CreatedAt = now,
                IsActive = false
            };

            var participants = new List<ConversationParticipant>
            {
                new ConversationParticipant
                {
                    Id = Guid.NewGuid().ToString(),
                    ConversationId = conversationId,
                    UserId = currentUserId,
                    JoinedAt = now,
                    LastReadAt = now,
                    Role = ConversationRole.Member.GetDescription()
                },
                new ConversationParticipant
                {
                    Id = Guid.NewGuid().ToString(),
                    ConversationId = conversationId,
                    UserId = otherUserId, JoinedAt = now,
                    LastReadAt = DateTime.MinValue,
                    Role = ConversationRole.Member.GetDescription()
                }
            };

            _context.Conversations.Add(newConversation);
            _context.ConversationParticipants.AddRange(participants);
            _context.ConversationKeys.Add(new ConversationKey { Id = Guid.NewGuid().ToString(), ConversationId = conversationId, UserLowId = userLow, UserHighId = userHigh });

            try
            {
                await _context.SaveChangesAsync(cancel);
            }
            catch (DbUpdateException)
            {
                _logger.LogWarning("Get or create direct conversation hit duplicate key between user {UserId} and user {OtherUserId}. Resolving existing conversation", currentUserId, otherUserId);
                foreach (var entry in _context.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged))
                {
                    entry.State = EntityState.Detached;
                }

                var existingConversationId = await _context.ConversationKeys.AsNoTracking()
                    .Where(k => k.UserLowId == userLow && k.UserHighId == userHigh && k.ConversationId != null)
                    .Select(k => k.ConversationId)
                    .FirstOrDefaultAsync(cancel);

                if (!string.IsNullOrWhiteSpace(existingConversationId))
                {
                    _logger.LogInformation("Direct conversation duplicate resolved between user {UserId} and user {OtherUserId}. ConversationId: {ConversationId}", currentUserId, otherUserId, existingConversationId);
                    return await GetConversationByIdAsync(currentUserId, existingConversationId, cancel);
                }

                throw;
            }

            var otherUserInfo = usersInfo[otherUserId];

            var isFriendDictionary = await GetFriendshipStatusesAsync(currentUserId, new List<string> { otherUserId });
            bool isFriend = isFriendDictionary.GetValueOrDefault(otherUserId, false);

            var onlineStatuses = await _redisService.GetUsersOnlineStatusAsync(new[] { otherUserId });
            bool isOnline = isFriend && onlineStatuses.GetValueOrDefault(otherUserId);

            var response = new ConversationResponse
            {
                Id = newConversation.Id,
                Type = newConversation.Type,
                Name = otherUserInfo.DisplayName,
                AvatarUrl = otherUserInfo.AvatarUrl,
                CreatedAt = newConversation.CreatedAt.ToUtc(),
                LastMessageAt = newConversation.LastMessageAt.ToUtc(),
                HasUnread = false,
                IsActive = newConversation.IsActive,
                LastMessage = null,
                Participants = participants.Select(p => MapParticipant(usersInfo[p.UserId], p.Role, p.LastReadMessageId, isOnline, isFriend)).ToList()
            };
            _logger.LogInformation("User {UserId} created direct conversation {ConversationId} with user {OtherUserId}", currentUserId, newConversation.Id, otherUserId);
            return response;
        }

        /// <inheritdoc />
        public Task<PaginatedData<ParticipantResponse>> GetGroupMembersAsync(string currentUserId, string conversationId, CursorPaginationRequest request, CancellationToken cancel)
        {
            return _conversationMemberService.GetGroupMembersAsync(currentUserId, conversationId, request, cancel);
        }

        #region Private Helpers

        // Helper DRY: Truy vấn thông tin cơ bản kèm Avatar giúp code gọn hơn đáng kể
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

        // Helper DRY: Chuyển đổi UserResponse thành ParticipantResponse
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

        // Helper DRY: Xử lý và Parse an toàn siêu dữ liệu (Metadata) hệ thống
        private SystemMessageMetadata? ParseSystemMetadata(string type, string? metadata)
        {
            if (type == "System" && !string.IsNullOrWhiteSpace(metadata))
            {
                try
                {
                    return JsonSerializer.Deserialize<SystemMessageMetadata>(metadata, _jsonCaseInsensitive);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error when deserialize message metadata");
                }
            }
            return null;
        }

        /// <summary>
        /// Kiểm tra danh sách người dùng xem ai là bạn bè của user hiện tại.
        /// </summary>
        /// <param name="currentUserId">ID của user đang đăng nhập.</param>
        /// <param name="userIdsToFetch">Danh sách các User ID cần kiểm tra.</param>
        /// <returns>Một Dictionary với Key là UserId và Value là true (nếu là bạn) hoặc false (nếu không).</returns>
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

            // 4. Build và trả về Dictionary
            return distinctUserIds.ToDictionary(
                userId => userId,
                userId => friendIdsSet.Contains(userId)
            );
        }

        #endregion
    }
}
