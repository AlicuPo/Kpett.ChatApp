using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Payload.Cursor;
using Kpett.ChatApp.DTOs.Request.Conversation;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.DTOs.Response.Conversation;
using Kpett.ChatApp.DTOs.Response.Conversation.Metadata;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.DTOs.Response.User;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Extentions;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Hubs;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Kpett.ChatApp.Services.Impls
{
    public class ConversationService : IConversationService
    {
        private readonly AppDbContext _context;
        private readonly IRedisService _redisService;
        private readonly IHubContext<ChatHub> _chatHubContext;
        private readonly ILogger<ConversationService> _logger;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public ConversationService(AppDbContext dbContext, IRedisService redisService, IHubContext<ChatHub> chatHubContext, ILogger<ConversationService> logger)
        {
            _context = dbContext;
            _redisService = redisService;
            _chatHubContext = chatHubContext;
            _logger = logger;
        }

        public async Task<ConversationResponse> CreateConversationAsync(string currentUserId, CreateConversationRequest request, CancellationToken cancel)
        {
            // 1. VALIDATE ĐẦU VÀO CƠ BẢN
            if (string.IsNullOrWhiteSpace(currentUserId))
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");

            if (request == null || string.IsNullOrWhiteSpace(request.Type))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Conversation request and type are required.");

            var isGroup = request.Type.Equals("Group", StringComparison.OrdinalIgnoreCase);
            var isDirect = request.Type.Equals("Direct", StringComparison.OrdinalIgnoreCase);

            if (!isGroup && !isDirect)
                throw new BadRequestException(ErrorCodes.VALIDATION.INVALID, "Invalid conversation type. Must be 'Direct' or 'Group'.");

            // Lọc trùng ID và tự động thêm ID của người đang thực hiện request
            var uniqueParticipantIds = new HashSet<string>(request.ParticipantIds.Where(id => !string.IsNullOrWhiteSpace(id)));
            uniqueParticipantIds.Add(currentUserId);

            // 2. VALIDATE LOGIC THEO TỪNG LOẠI
            if (isDirect)
            {
                if (uniqueParticipantIds.Count != 2)
                    throw new BadRequestException(ErrorCodes.VALIDATION.INVALID, "Direct conversation must have exactly 2 different participants.");

                if (string.IsNullOrWhiteSpace(request.InitialMessage))
                    throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Initial message content is required to create a direct conversation.");
            }
            else if (isGroup)
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Group name is required.");

                if (uniqueParticipantIds.Count < 2)
                    throw new BadRequestException(ErrorCodes.VALIDATION.INVALID, "A group must have at least 2 participants.");
            }

            var now = DateTime.UtcNow;

            // 3. TRUY VẤN USERS SỚM LẤY ĐẦY ĐỦ THÔNG TIN
            var usersInfo = await _context.Users
                .AsNoTracking()
                .Where(u => uniqueParticipantIds.Contains(u.Id))
                .Select(u => new
                {
                    u.Id,
                    DisplayName = u.DisplayName ?? u.Username,
                    u.Username,
                    AvatarUrl = _context.UserMedias
                        .Where(um => um.UserId == u.Id && um.IsPrimary && um.MediaType == UserMediaType.Avatar.GetDescription())
                        .Select(um => um.MediaUrl)
                        .FirstOrDefault()
                })
                .ToDictionaryAsync(u => u.Id, u => u, cancel);

            if (usersInfo.Count != uniqueParticipantIds.Count)
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "One or more participants do not exist.");

            // 4. XỬ LÝ RIÊNG CHO DIRECT CHAT (Kiểm tra và tái sử dụng phòng cũ)
            if (isDirect)
            {
                var participantsArray = uniqueParticipantIds.ToArray();
                bool isOrderCorrect = string.CompareOrdinal(participantsArray[0], participantsArray[1]) < 0;
                var userLow = isOrderCorrect ? participantsArray[0] : participantsArray[1];
                var userHigh = isOrderCorrect ? participantsArray[1] : participantsArray[0];

                // Truy vấn Key xem phòng đã tồn tại chưa
                var existingKey = await _context.ConversationKeys
                    .FirstOrDefaultAsync(k => k.UserLowId == userLow && k.UserHighId == userHigh && k.ConversationId != null, cancel);

                if (existingKey != null)
                {
                    var existingConversation = await _context.Conversations
                        .FirstOrDefaultAsync(c => c.Id == existingKey.ConversationId, cancel);

                    if (existingConversation != null)
                    {
                        // Thêm tin nhắn mới vào phòng cũ
                        var additionalMessage = new Message
                        {
                            Id = Guid.NewGuid().ToString(),
                            ConversationId = existingConversation.Id,
                            SenderId = currentUserId,
                            Content = request.InitialMessage,
                            Type = MessageType.Text.GetDescription(),
                            CreatedAt = now
                        };

                        _context.Messages.Add(additionalMessage);
                        existingConversation.LastMessageAt = now;

                        // Lấy thông tin thành viên KHÔNG dùng Navigation Property
                        var existingDbParticipants = await _context.ConversationParticipants
                            .Where(p => p.ConversationId == existingConversation.Id)
                            .Select(p => new { p.UserId, p.Role, p.LastReadMessageId })
                            .ToListAsync(cancel);

                        await _context.SaveChangesAsync(cancel);

                        // Map thủ công trong RAM kèm Username và AvatarUrl
                        var onlineStatuses = await _redisService.GetUsersOnlineStatusAsync(uniqueParticipantIds);
                        var mappedParticipants = existingDbParticipants.Select(p =>
                        {
                            var uInfo = usersInfo.GetValueOrDefault(p.UserId);
                            return new ParticipantResponse
                            {
                                Id = p.UserId,
                                DisplayName = uInfo?.DisplayName ?? "Người dùng",
                                Username = uInfo?.Username ?? "unknown",
                                AvatarUrl = uInfo?.AvatarUrl,
                                Role = p.Role,
                                IsOnline = onlineStatuses.TryGetValue(p.UserId, out var isOnline) && isOnline,
                                LastReadMessageId = p.LastReadMessageId
                            };
                        }).ToList();

                        var reusedConversationResponse = new ConversationResponse
                        {
                            Id = existingConversation.Id,
                            Type = existingConversation.Type,
                            Name = existingConversation.Name,
                            AvatarUrl = existingConversation.AvatarUrl,
                            CreatedAt = existingConversation.CreatedAt.ToUtc(),
                            LastMessageAt = existingConversation.LastMessageAt.ToUtc(),
                            LastMessage = new MessageSnippetResponse
                            {
                                Id = additionalMessage.Id,
                                SenderId = additionalMessage.SenderId,
                                Content = additionalMessage.Content,
                                Type = additionalMessage.Type,
                                CreatedAt = additionalMessage.CreatedAt,
                            },
                            Participants = mappedParticipants
                        };

                        // ------------------------------------------------------------------
                        // [SIGNALR PUSH] Bắn tin nhắn mới vào phòng cũ & Update Conversation
                        // ------------------------------------------------------------------
                        try
                        {
                            // Bắn tin nhắn vào Group chat hiện tại
                            await _chatHubContext.Clients.Group(existingConversation.Id).SendAsync("ReceiveNewMessage", reusedConversationResponse.LastMessage, cancel);

                            // Đồng thời báo cho cả 2 user đẩy Conversation này lên đầu danh sách
                            foreach (var userId in uniqueParticipantIds)
                            {
                                await _chatHubContext.Clients.User(userId).SendAsync("ConversationUpdated", reusedConversationResponse, cancel);
                            }
                        }
                        catch { /* Log lỗi SignalR */ }

                        return reusedConversationResponse;
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
                CreatedAt = now
            };

            // Xử lý loại tin nhắn và Metadata đa hình
            string? metadataJson = null;
            var isSystemMessage = isGroup && string.IsNullOrWhiteSpace(request.InitialMessage);
            var messageType = MessageType.Text.GetDescription();
            var messageContent = request.InitialMessage;

            if (isGroup && isSystemMessage)
            {
                messageType = MessageType.System.GetDescription();
                messageContent = "Đã tạo nhóm";

                var actorSnapshot = new SnapshotUser
                {
                    Id = currentUserId,
                    Name = usersInfo.GetValueOrDefault(currentUserId)?.DisplayName ?? "Một thành viên"
                };

                var targetSnapshots = uniqueParticipantIds
                    .Where(id => id != currentUserId)
                    .Select(id => new SnapshotUser { Id = id, Name = usersInfo.GetValueOrDefault(id)?.DisplayName ?? "người dùng" })
                    .ToList();

                SystemMessageMetadata systemMetadata = new GroupCreatedMetadata
                {
                    Actor = actorSnapshot,
                    Targets = targetSnapshots
                };

                metadataJson = JsonSerializer.Serialize(systemMetadata, _jsonOptions);
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

            // Khởi tạo danh sách người tham gia
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

            await _context.SaveChangesAsync(cancel);

            SystemMessageMetadata? metadata = null;
            if (initialMessage.Type == "System" && !string.IsNullOrWhiteSpace(initialMessage.Metadata))
            {
                try { metadata = JsonSerializer.Deserialize<SystemMessageMetadata>(initialMessage.Metadata, _jsonOptions); }
                catch { }
            }

            var newConversationResponse = new ConversationResponse
            {
                Id = newConversation.Id,
                Type = newConversation.Type,
                Name = newConversation.Name,
                AvatarUrl = newConversation.AvatarUrl,
                CreatedAt = newConversation.CreatedAt.ToUtc(),
                LastMessageAt = newConversation.LastMessageAt.ToUtc(),
                LastMessage = new MessageSnippetResponse
                {
                    Id = initialMessage.Id,
                    SenderId = initialMessage.SenderId,
                    Content = initialMessage.Content,
                    Type = initialMessage.Type,
                    ActionMetadata = metadata,
                    CreatedAt = initialMessage.CreatedAt
                },
                Participants = participants.Select(p =>
                {
                    var uInfo = usersInfo.GetValueOrDefault(p.UserId);
                    return new ParticipantResponse
                    {
                        Id = p.UserId,
                        DisplayName = uInfo?.DisplayName ?? "Người dùng",
                        Username = uInfo?.Username ?? "unknown",
                        AvatarUrl = uInfo?.AvatarUrl,
                        Role = p.Role
                    };
                }).ToList()
            };

            // ------------------------------------------------------------------
            // [SIGNALR PUSH] Bắn sự kiện tạo phòng mới cho TẤT CẢ participant
            // ------------------------------------------------------------------
            try
            {
                foreach (var userId in uniqueParticipantIds)
                {
                    // Dùng Clients.User vì lúc này phòng mới tinh, các client khác chưa Join() vào SignalR Group này
                    await _chatHubContext.Clients.User(userId).SendAsync("NewConversationCreated", newConversationResponse, cancel);
                }
            }
            catch { /* Log lỗi SignalR */ }

            return newConversationResponse;
        }

        /// <summary>
        /// Lấy danh sách hội thoại của người dùng hiện tại, hỗ trợ phân trang bằng Cursor và tìm kiếm.
        /// </summary>
        public async Task<PaginatedData<ConversationResponse>> GetConversationsAsync(string currentUserId, ConversationListRequest request, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(currentUserId))
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");

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

            // Khởi tạo Base Query
            var baseQuery = _context.ConversationParticipants.AsNoTracking()
                .Where(cp => cp.UserId == currentUserId)
                .Join(_context.Conversations.AsNoTracking(),
                    cp => cp.ConversationId,
                    c => c.Id,
                    (cp, c) => new { Participant = cp, Conversation = c });

            // LỌC TÌM KIẾM (SEARCH)
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                baseQuery = baseQuery.Where(x =>
                    (x.Conversation.Type == "Group" && x.Conversation.Name != null && x.Conversation.Name.Contains(searchTerm)) ||
                    (x.Conversation.Type == "Direct" && _context.ConversationParticipants.Any(p =>
                        p.ConversationId == x.Conversation.Id &&
                        p.UserId != currentUserId &&
                        _context.Users.Any(u => u.Id == p.UserId &&
                            ((u.DisplayName != null && u.DisplayName.Contains(searchTerm)) ||
                             (u.Username != null && u.Username.Contains(searchTerm))))
                    ))
                );
            }

            // LỌC THEO CURSOR (KEYSET PAGINATION)
            if (cursorDate.HasValue && !string.IsNullOrWhiteSpace(cursorId))
            {
                baseQuery = baseQuery.Where(x =>
                    x.Conversation.LastMessageAt < cursorDate.Value ||
                    (x.Conversation.LastMessageAt == cursorDate.Value && string.Compare(x.Conversation.Id, cursorId) < 0));
            }

            // TỐI ƯU HÓA PROJECTION
            var projectionQuery = baseQuery.Select(x => new
            {
                x.Conversation.Id,
                x.Conversation.Type,
                x.Conversation.Name,
                x.Conversation.AvatarUrl,
                x.Conversation.CreatedAt,
                x.Conversation.LastMessageAt,
                x.Participant.LastReadAt,

                LastMessage = _context.Messages
                    .Where(m => m.ConversationId == x.Conversation.Id)
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => new
                    {
                        m.Id,
                        m.SenderId,
                        m.Type,
                        m.Content,
                        m.CreatedAt,
                        m.Metadata
                    })
                    .FirstOrDefault(),

                // Xác định ID và Role của người đối diện cho Direct Chat
                OtherParticipant = x.Conversation.Type == "Direct"
                    ? _context.ConversationParticipants
                        .Where(p => p.ConversationId == x.Conversation.Id && p.UserId != currentUserId)
                        .Select(p => new { p.UserId, p.Role, p.LastReadMessageId })
                        .FirstOrDefault()
                    : null
            });

            var rawConversations = await projectionQuery
                .OrderByDescending(x => x.LastMessageAt)
                .ThenByDescending(x => x.Id)
                .Take(limit + 1)
                .ToListAsync(cancel);

            // XỬ LÝ PHÂN TRANG
            string? nextCursor = null;
            var itemsToProcess = rawConversations;

            if (rawConversations.Count > limit)
            {
                var lastItem = rawConversations[limit - 1];
                nextCursor = CursorHelper.Encode(new ConversationCursorPayload
                {
                    ConversationId = lastItem.Id,
                    LastMessageAt = lastItem.LastMessageAt
                });

                itemsToProcess = rawConversations.Take(limit).ToList();
            }

            var userIdsToFetch = new HashSet<string>();
            var groupMembersDict = new Dictionary<string, List<(string UserId, string Role, string LastReadMessageId)>>();

            // Gom ID của đối tác Direct Chat
            var directParticipants = itemsToProcess.Where(c => c.Type == "Direct" && c.OtherParticipant != null).Select(c => c.OtherParticipant!).ToList();
            foreach (var p in directParticipants) userIdsToFetch.Add(p.UserId);

            // Gom ID của người gửi tin nhắn cuối cùng
            foreach (var c in itemsToProcess.Where(c => c.LastMessage != null)) userIdsToFetch.Add(c.LastMessage!.SenderId);

            // Fetch 3 thành viên đại diện cho TẤT CẢ các Group (Để hiển thị Preview Participant)
            var groupIds = itemsToProcess.Where(c => c.Type == "Group").Select(c => c.Id).ToList();
            if (groupIds.Any())
            {
                var groupParticipants = await _context.ConversationParticipants.AsNoTracking()
                    .Where(p => groupIds.Contains(p.ConversationId))
                    .GroupBy(p => p.ConversationId)
                    .Select(g => new
                    {
                        ConversationId = g.Key,
                        Participants = g.Where(p => !p.IsKicked).Select(p => new { p.UserId, p.Role, p.LastReadMessageId }).Take(3).ToList()
                    })
                    .ToListAsync(cancel);

                foreach (var gp in groupParticipants)
                {
                    groupMembersDict[gp.ConversationId] = gp.Participants.Select(p => (p.UserId, p.Role, p.LastReadMessageId)).ToList();
                    foreach (var p in gp.Participants) userIdsToFetch.Add(p.UserId);
                }
            }

            // Gọi DB Lần 2: Lấy thông tin User (Bao gồm Username và Avatar)
            var usersDict = new Dictionary<string, UserResponse>();
            if (userIdsToFetch.Any())
            {
                usersDict = await _context.Users.AsNoTracking()
                    .Where(u => userIdsToFetch.Contains(u.Id))
                    .Select(u => new UserResponse
                    {
                        Id = u.Id,
                        DisplayName = u.DisplayName ?? u.Username,
                        Username = u.Username,
                        AvatarUrl = _context.UserMedias
                            .Where(m => m.UserId == u.Id && m.IsPrimary == true && m.MediaType == UserMediaType.Avatar.GetDescription())
                            .Select(m => m.MediaUrl)
                            .FirstOrDefault()
                    })
                    .ToDictionaryAsync(u => u.Id, u => u, cancel);
            }

            // Gọi Redis lấy trạng thái Online
            var directOtherIds = directParticipants.Select(p => p.UserId).ToList();
            var onlineStatuses = await _redisService.GetUsersOnlineStatusAsync(directOtherIds);
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var items = itemsToProcess.Select(c =>
            {
                var isDirect = c.Type == "Direct";
                var isGroup = c.Type == "Group";

                string? displayName = c.Name;
                string? avatarUrl = c.AvatarUrl;
                bool isOnline = false;
                var participants = new List<ParticipantResponse>();

                // Map Dữ liệu cho Direct Chat
                if (isDirect && c.OtherParticipant != null && usersDict.TryGetValue(c.OtherParticipant.UserId, out var otherUser))
                {
                    displayName = otherUser.DisplayName;
                    avatarUrl = otherUser.AvatarUrl;
                    isOnline = onlineStatuses.TryGetValue(c.OtherParticipant.UserId, out var status) && status;

                    participants.Add(new ParticipantResponse
                    {
                        Id = otherUser.Id,
                        DisplayName = otherUser.DisplayName,
                        Username = otherUser.Username,
                        AvatarUrl = otherUser.AvatarUrl,
                        Role = c.OtherParticipant.Role,
                        IsOnline = isOnline,
                        LastReadMessageId = c.OtherParticipant.LastReadMessageId
                    });
                }

                // Map Dữ liệu cho Group
                if (isGroup && groupMembersDict.TryGetValue(c.Id, out var members))
                {
                    foreach (var m in members)
                    {
                        if (usersDict.TryGetValue(m.UserId, out var memberInfo))
                        {
                            participants.Add(new ParticipantResponse
                            {
                                Id = memberInfo.Id,
                                DisplayName = memberInfo.DisplayName,
                                Username = memberInfo.Username,
                                AvatarUrl = memberInfo.AvatarUrl,
                                Role = m.Role,
                                LastReadMessageId = m.LastReadMessageId,
                                IsOnline = onlineStatuses.TryGetValue(memberInfo.Id, out var status) && status
                            });
                        }
                    }
                }

                // Xử lý Last Message
                MessageSnippetResponse? lastMessageResponse = null;
                if (c.LastMessage != null)
                {
                    string? senderName = usersDict.TryGetValue(c.LastMessage.SenderId, out var sender)
                        ? sender.DisplayName
                        : "Unknown User";

                    SystemMessageMetadata? metadata = null;
                    if (c.LastMessage.Type == "System" && !string.IsNullOrWhiteSpace(c.LastMessage.Metadata))
                    {
                        try { metadata = JsonSerializer.Deserialize<SystemMessageMetadata>(c.LastMessage.Metadata, jsonOptions); }
                        catch { }
                    }

                    lastMessageResponse = new MessageSnippetResponse
                    {
                        Id = c.LastMessage.Id,
                        SenderId = c.LastMessage.SenderId,
                        SenderName = senderName,
                        Type = c.LastMessage.Type,
                        Content = c.LastMessage.Content,
                        CreatedAt = c.LastMessage.CreatedAt,
                        ActionMetadata = metadata
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
                    HasUnread = !c.LastReadAt.HasValue || c.LastReadAt.Value == DateTime.MinValue || c.LastMessageAt > c.LastReadAt.Value,
                    LastMessage = lastMessageResponse,
                    Participants = participants,
                };
            }).ToList();

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

        public async Task<bool> AddMembersToGroupAsync(string currentUserId, AddMembersRequest request, CancellationToken cancel)
        {
            // Validate đầu vào cơ bản
            if (string.IsNullOrWhiteSpace(currentUserId))
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");

            if (request.UserIdsToAdd == null || !request.UserIdsToAdd.Any())
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "User IDs to add are required.");

            var uniqueUserIdsToAdd = request.UserIdsToAdd
                .Where(id => !string.IsNullOrWhiteSpace(id) && id != currentUserId)
                .Distinct()
                .ToList();

            if (!uniqueUserIdsToAdd.Any())
                throw new BadRequestException(ErrorCodes.VALIDATION.INVALID, "No valid users to add.");

            // Kiểm tra tính hợp lệ của Group và quyền
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == request.ConversationId && c.Type == "Group", cancel);

            if (conversation == null)
                throw new NotFoundException(ErrorCodes.CONVERSATION.NOT_FOUND, "Group conversation not found.");

            var isCurrentUserMember = await _context.ConversationParticipants
                .AnyAsync(cp => cp.ConversationId == request.ConversationId && cp.UserId == currentUserId && !cp.IsKicked, cancel);

            if (!isCurrentUserMember)
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You are not an active member of this group.");

            // XỬ LÝ LỌC TRÙNG VÀ TÁI KÍCH HOẠT
            var existingParticipants = await _context.ConversationParticipants
                .Where(cp => cp.ConversationId == request.ConversationId && uniqueUserIdsToAdd.Contains(cp.UserId))
                .ToListAsync(cancel);

            var activeMemberIds = existingParticipants
                .Where(cp => !cp.IsKicked)
                .Select(cp => cp.UserId)
                .ToHashSet();

            var userIdsToProcess = uniqueUserIdsToAdd.Where(id => !activeMemberIds.Contains(id)).ToList();

            if (!userIdsToProcess.Any())
                throw new BadRequestException(ErrorCodes.VALIDATION.INVALID, "All provided users are already active members in the group.");

            // Lấy thông tin hiển thị (Display Name)
            var allUserIdsToFetch = userIdsToProcess.Concat(new[] { currentUserId }).Distinct().ToList();

            var usersDictionary = await _context.Users
                .AsNoTracking()
                .Where(u => allUserIdsToFetch.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName ?? u.Username, cancel);

            if (usersDictionary.Count(x => userIdsToProcess.Contains(x.Key)) != userIdsToProcess.Count)
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "One or more users to add do not exist.");

            var now = DateTime.UtcNow;
            string memberRole = ConversationRole.Member.GetDescription();

            // PHÂN LOẠI UPDATE VÀ INSERT
            var participantsToReactivate = existingParticipants.Where(cp => cp.IsKicked && userIdsToProcess.Contains(cp.UserId)).ToList();
            foreach (var participant in participantsToReactivate)
            {
                participant.IsKicked = false;
                participant.JoinedAt = now;
                participant.LastReadAt = DateTime.MinValue;
                participant.Role = memberRole;
            }

            var reactivatedUserIds = participantsToReactivate.Select(p => p.UserId).ToHashSet();
            var newParticipants = userIdsToProcess
                .Where(id => !reactivatedUserIds.Contains(id))
                .Select(userId => new ConversationParticipant
                {
                    Id = Guid.NewGuid().ToString(),
                    ConversationId = request.ConversationId,
                    UserId = userId,
                    JoinedAt = now,
                    LastReadAt = DateTime.MinValue,
                    Role = memberRole
                }).ToList();

            // Tạo Metadata và Tin nhắn hệ thống
            string adderName = usersDictionary.GetValueOrDefault(currentUserId, "Một thành viên")!;
            var addedNamesList = userIdsToProcess.Select(id => usersDictionary[id]).ToList();
            string addedNamesString = string.Join(", ", addedNamesList);

            var actorSnapshot = new SnapshotUser { Id = currentUserId, Name = adderName };
            var targetSnapshots = userIdsToProcess
                .Select(id => new SnapshotUser { Id = id, Name = usersDictionary[id] })
                .ToList();

            SystemMessageMetadata systemMetadata = new MemberAddedMetadata
            {
                Actor = actorSnapshot,
                Targets = targetSnapshots
            };

            var systemMessage = new Message
            {
                Id = Guid.NewGuid().ToString(),
                ConversationId = request.ConversationId,
                SenderId = currentUserId,
                Type = MessageType.System.GetDescription(),
                Content = $"{adderName} đã thêm {addedNamesString} vào nhóm.",
                Metadata = JsonSerializer.Serialize(systemMetadata, _jsonOptions),
                CreatedAt = now
            };

            var currentUserParticipant = await _context.ConversationParticipants.FirstOrDefaultAsync(p => p.UserId == currentUserId);
            if (currentUserParticipant != null)
            {
                currentUserParticipant.LastReadMessageId = systemMessage.Id;
                currentUserParticipant.LastReadAt = now;
                _context.ConversationParticipants.Update(currentUserParticipant);
            }

            conversation.LastMessageAt = now;

            if (newParticipants.Any())
            {
                _context.ConversationParticipants.AddRange(newParticipants);
            }

            _context.Messages.Add(systemMessage);
            await _context.SaveChangesAsync(cancel);

            try
            {
                // Map tin nhắn hệ thống ra Response hoàn chỉnh
                var msgResponse = await MapToMessageResponseAsync(systemMessage.Id, currentUserId, cancel);

                // Bắn Message hệ thống cho Group hiện tại
                await _chatHubContext.Clients.Group(request.ConversationId).SendAsync("ReceiveNewMessage", msgResponse, cancel);

                // Bắn event báo cho các cá nhân MỚI được thêm vào.
                foreach (var newUserId in userIdsToProcess)
                {
                    await _chatHubContext.Clients.User(newUserId).SendAsync("AddedToConversation", request.ConversationId, cancel);
                }
            }
            catch { /* Log lỗi SignalR */ }

            return true;
        }

        public async Task<bool> RemoveMemberFromGroupAsync(string currentUserId, string conversationId, string userIdToRemove, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(currentUserId) || string.IsNullOrWhiteSpace(userIdToRemove))
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "Invalid user info.");

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.Type == "Group", cancel);

            if (conversation == null)
                throw new NotFoundException(ErrorCodes.CONVERSATION.NOT_FOUND, "Group conversation not found.");

            var participants = await _context.ConversationParticipants
                .Where(cp => cp.ConversationId == conversationId && (cp.UserId == currentUserId || cp.UserId == userIdToRemove))
                .ToListAsync(cancel);

            var currentUserParticipant = participants.FirstOrDefault(cp => cp.UserId == currentUserId);
            if (currentUserParticipant == null)
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You are not a member of this group.");

            var targetParticipant = participants.FirstOrDefault(cp => cp.UserId == userIdToRemove);
            if (targetParticipant == null)
                throw new NotFoundException(ErrorCodes.CONVERSATION.NOT_FOUND, "The target user is not a member of this group.");

            var usersInfo = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == currentUserId || u.Id == userIdToRemove)
                .Select(u => new { u.Id, DisplayName = u.DisplayName ?? u.Username })
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancel);

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

                systemMetadata = new MemberLeftMetadata
                {
                    Actor = actorSnapshot
                };
            }
            else
            {
                string ownerRole = ConversationRole.Owner.GetDescription();
                string modRole = ConversationRole.Moderator.GetDescription();

                bool hasPermissionToKick = currentUserParticipant.Role == modRole || currentUserParticipant.Role == ownerRole;

                if (!hasPermissionToKick)
                    throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You don't have permission to remove members.");

                systemContent = $"{removerName} đã xóa {removedName} khỏi nhóm.";
                targetParticipant.IsKicked = true;
                _context.ConversationParticipants.Update(targetParticipant);

                systemMetadata = new MemberRemovedMetadata
                {
                    Actor = actorSnapshot,
                    Targets = new List<SnapshotUser>
                    {
                        new SnapshotUser { Id = userIdToRemove, Name = removedName }
                    }
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

                Metadata = JsonSerializer.Serialize(systemMetadata, _jsonOptions),
                CreatedAt = now
            };

            currentUserParticipant.LastReadMessageId = systemMessage.Id;
            currentUserParticipant.LastReadAt = now;
            _context.ConversationParticipants.Update(currentUserParticipant);

            conversation.LastMessageAt = now;

            _context.Messages.Add(systemMessage);
            await _context.SaveChangesAsync(cancel);

            // [SIGNALR PUSH]
            try
            {
                await _chatHubContext.Clients.User(userIdToRemove).SendAsync("RemovedFromConversation", conversationId, cancel);

                var msgResponse = await MapToMessageResponseAsync(systemMessage.Id, currentUserId, cancel);
                await _chatHubContext.Clients.Group(conversationId).SendAsync("ReceiveNewMessage", msgResponse, cancel);
            }
            catch { /* Log lỗi SignalR */ }

            return true;
        }

        public async Task<PaginatedData<MessageResponse>> GetMessagesAsync(string currentUserId, string conversationId, MessageListRequest request, CancellationToken cancel)
        {
            // VALIDATE ĐẦU VÀO CƠ BẢN
            if (string.IsNullOrWhiteSpace(currentUserId))
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");

            if (string.IsNullOrWhiteSpace(conversationId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Conversation ID is required.");

            var limit = request.Limit <= 0 ? 20 : Math.Min(request.Limit, 50);

            bool isParticipant = await _context.ConversationParticipants
                .AnyAsync(p => p.ConversationId == conversationId && p.UserId == currentUserId && !p.IsKicked, cancel);

            if (!isParticipant)
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You do not have permission to view messages in this conversation.");

            DateTime? cursorDate = null;
            string? cursorId = null;

            if (!string.IsNullOrWhiteSpace(request.Cursor))
            {
                var decoded = CursorHelper.Decode<MessageCursorPayload>(request.Cursor);
                if (decoded != null)
                {
                    cursorDate = decoded.CreatedAt;
                    cursorId = decoded.MessageId;
                }
            }

            // XÂY DỰNG TRUY VẤN CƠ BẢN
            var baseQuery = _context.Messages.AsNoTracking()
                .Where(m => m.ConversationId == conversationId);

            // Lọc tin nhắn CŨ HƠN Cursor (Dùng cho thao tác cuộn lên xem lịch sử)
            if (cursorDate.HasValue && !string.IsNullOrWhiteSpace(cursorId))
            {
                baseQuery = baseQuery.Where(m =>
                    m.CreatedAt < cursorDate.Value ||
                    (m.CreatedAt == cursorDate.Value && string.Compare(m.Id, cursorId) < 0));
            }

            // Sắp xếp giảm dần (Mới nhất lên đầu) để lấy đúng 'Limit' tin nhắn gần nhất tính từ Cursor
            var query = baseQuery
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .Take(limit + 1)
                .Select(m => new
                {
                    m.Id,
                    m.SenderId,
                    m.Type,
                    m.Content,
                    m.CreatedAt,
                    m.Metadata,
                    SenderName = _context.Users
                        .Where(u => u.Id == m.SenderId)
                        .Select(u => u.DisplayName ?? u.Username)
                        .FirstOrDefault(),
                    SenderAvatarUrl = _context.UserMedias
                        .Where(um => um.UserId == m.SenderId && um.IsPrimary == true && um.MediaType == UserMediaType.Avatar.GetDescription())
                        .Select(um => um.MediaUrl)
                        .FirstOrDefault()
                });

            var rawMessages = await query.ToListAsync(cancel);

            // XỬ LÝ PHÂN TRANG (PAGINATION)
            string? nextCursor = null;
            var itemsToProcess = rawMessages;

            // Nếu lấy dư 1 record -> chứng tỏ còn trang tiếp theo
            if (rawMessages.Count > limit)
            {
                // Lấy phần tử cuối cùng của danh sách HIỂN THỊ làm mốc cho lần gọi tiếp theo
                var lastItem = rawMessages[limit - 1];
                nextCursor = CursorHelper.Encode(new MessageCursorPayload
                {
                    MessageId = lastItem.Id,
                    CreatedAt = lastItem.CreatedAt
                });

                itemsToProcess = rawMessages.Take(limit).ToList();
            }

            // MAPPING DỮ LIỆU SANG DTO VÀ XỬ LÝ METADATA
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var messageResponses = itemsToProcess.Select(m =>
            {
                SystemMessageMetadata? actionMetadata = null;

                // Nếu là tin nhắn hệ thống, giải mã Metadata JSON để frontend tự render UI phù hợp
                if (m.Type == MessageType.System.GetDescription() && !string.IsNullOrWhiteSpace(m.Metadata))
                {
                    try
                    {
                        actionMetadata = JsonSerializer.Deserialize<SystemMessageMetadata>(m.Metadata, jsonOptions);
                    }
                    catch { /* Log lỗi giải mã nếu cần */ }
                }

                return new MessageResponse
                {
                    Id = m.Id,
                    SenderId = m.SenderId,
                    SenderName = m.SenderName ?? "Unknown User",
                    SenderAvatarUrl = m.SenderAvatarUrl,
                    Type = m.Type,
                    Content = m.Content,
                    CreatedAt = m.CreatedAt.ToUtc(),
                    ActionMetadata = actionMetadata
                };
            }).ToList();

            return new PaginatedData<MessageResponse>
            {
                Items = messageResponses,
                Pagination = new CursorPaginationMeta
                {
                    NextCursor = nextCursor,
                    Limit = limit
                }
            };
        }

        public async Task<MessageResponse> SendMessageAsync(string currentUserId, string conversationId, SendMessageRequest request, CancellationToken cancel)
        {
            // VALIDATE DỮ LIỆU ĐẦU VÀO
            if (string.IsNullOrWhiteSpace(currentUserId))
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");

            if (string.IsNullOrWhiteSpace(request.ClientMessageId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "ClientMessageId is required for idempotency.");

            if (string.IsNullOrWhiteSpace(request.Content) && (request.Attachments == null || !request.Attachments.Any()))
                throw new BadRequestException(ErrorCodes.VALIDATION.INVALID, "Message must contain either text content or attachments.");

            // CHỐNG TRÙNG LẶP (IDEMPOTENCY)
            // Kiểm tra xem tin nhắn với ClientMessageId này đã được lưu vào DB chưa (do mạng chập chờn client gọi lại 2 lần)
            var existingMessage = await _context.Messages.AsNoTracking()
                .Where(m => m.ConversationId == conversationId && m.ClientMessageId == request.ClientMessageId)
                .Select(m => new { m.Id, m.SenderId, m.Type, m.Content, m.CreatedAt, m.Metadata })
                .FirstOrDefaultAsync(cancel);

            if (existingMessage != null)
            {
                // Đã tồn tại -> Trả về kết quả ngay, không báo lỗi, không tạo mới
                return await MapToMessageResponseAsync(existingMessage.Id, currentUserId, cancel);
            }

            // KIỂM TRA QUYỀN TRUY CẬP (BẢO MẬT)
            var participantInfo = await _context.ConversationParticipants
                .Where(p => p.ConversationId == conversationId && p.UserId == currentUserId && !p.IsKicked)
                .FirstOrDefaultAsync(cancel);

            if (participantInfo == null)
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You are not a participant of this conversation.");

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId, cancel);

            if (conversation == null)
                throw new NotFoundException(ErrorCodes.CONVERSATION.NOT_FOUND, "Conversation not found.");

            var now = DateTime.UtcNow;
            var messageId = Guid.NewGuid().ToString();

            // XỬ LÝ ATTACHMENTS THÀNH JSON METADATA (Hoặc lưu bảng riêng tùy thiết kế)
            string? metadataJson = null;
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            if (request.Attachments != null && request.Attachments.Any())
            {
                var mediaMetadata = new { Attachments = request.Attachments };
                metadataJson = JsonSerializer.Serialize(mediaMetadata, jsonOptions);
            }

            // KHỞI TẠO ENTITY TIN NHẮN
            var newMessage = new Message
            {
                Id = messageId,
                ConversationId = conversationId,
                SenderId = currentUserId,
                ClientMessageId = request.ClientMessageId,
                Type = request.Type,
                Content = request.Content,
                ReplyToMessageId = request.ReplyToMessageId,
                Metadata = metadataJson,
                CreatedAt = now
            };

            //CẬP NHẬT TRẠNG THÁI HỘI THOẠI & PARTICIPANT
            conversation.LastMessageAt = now;

            // Tự động đánh dấu người gửi đã đọc tin nhắn mới nhất của chính họ
            participantInfo.LastReadAt = now;
            participantInfo.LastReadMessageId = messageId;

            _context.Messages.Add(newMessage);
            await _context.SaveChangesAsync(cancel);

            // BUILD RESPONSE CHUẨN ĐỂ TRẢ VỀ VÀ BẮN SIGNALR
            var responseDto = await MapToMessageResponseAsync(messageId, currentUserId, cancel);

            //ĐẨY SỰ KIỆN QUA SIGNALR
            try
            {
                var participantIds = await _context.ConversationParticipants
                    .Where(p => p.ConversationId == conversationId && !p.IsKicked)
                    .Select(p => p.UserId)
                    .ToListAsync(cancel);

                await _chatHubContext.Clients.Users(participantIds).SendAsync("ReceiveNewMessage", responseDto, cancel);
            }
            catch
            {
                // Log lỗi SignalR
            }

            return responseDto;
        }

        public async Task MarkAsReadAsync(string conversationId, string currentUserId, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(currentUserId))
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");

            if (string.IsNullOrWhiteSpace(conversationId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "ConversationId is required.");

            // Tìm thông tin thành viên (Participant) trong phòng chat
            var participant = await _context.ConversationParticipants
                .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == currentUserId && !p.IsKicked, cancel);

            if (participant == null)
            {
                return; // Nếu không phải thành viên thì bỏ qua
            }

            // Tìm ID của tin nhắn mới nhất trong phòng này
            var latestMessageId = await _context.Messages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => m.Id)
                .FirstOrDefaultAsync(cancel);

            // Cập nhật trạng thái
            var now = DateTime.UtcNow;
            participant.LastReadAt = now;

            if (latestMessageId != null)
            {
                participant.LastReadMessageId = latestMessageId;
            }

            _context.ConversationParticipants.Update(participant);
            await _context.SaveChangesAsync(cancel);

            // BẮN SỰ KIỆN "ĐÃ XEM" LÊN CHO TẤT CẢ NGƯỜI TRONG PHÒNG
            if (latestMessageId != null)
            {
                try
                {
                    var otherUserIds = await _context.ConversationParticipants
                        .Where(p => p.ConversationId == conversationId && p.UserId != currentUserId && !p.IsKicked)
                        .Select(p => p.UserId)
                        .ToListAsync(cancel);

                    if (otherUserIds.Any())
                    {
                        await _chatHubContext.Clients.Users(otherUserIds)
                            .SendAsync("UserReadMessage", conversationId, currentUserId, latestMessageId, cancel);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[SignalR Error] Lỗi khi gửi UserReadMessage: {ex.Message}");
                }
            }
        }

        // Hàm phụ trợ để Map Dữ liệu không dùng Navigation Property
        private async Task<MessageResponse> MapToMessageResponseAsync(string messageId, string currentUserId, CancellationToken cancel)
        {
            var messageData = await _context.Messages.AsNoTracking()
                .Where(m => m.Id == messageId)
                .Select(m => new
                {
                    m.Id,
                    m.ConversationId,
                    m.ClientMessageId,
                    m.SenderId,
                    m.Type,
                    m.Content,
                    m.CreatedAt,
                    m.Metadata,
                    m.ReplyToMessageId,
                    SenderName = _context.Users.Where(u => u.Id == m.SenderId).Select(u => u.DisplayName ?? u.Username).FirstOrDefault(),
                    SenderAvatarUrl = _context.UserMedias.Where(um => um.UserId == m.SenderId && um.IsPrimary == true && um.MediaType == "Avatar").Select(um => um.MediaUrl).FirstOrDefault()
                })
                .FirstOrDefaultAsync(cancel);

            // Giải mã Attachments từ Metadata (nếu có)
            List<MessageAttachmentResponse>? attachments = null;
            if (!string.IsNullOrWhiteSpace(messageData.Metadata) && (messageData.Type == "Image" || messageData.Type == "Video" || messageData.Type == "File"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(messageData.Metadata);
                    if (doc.RootElement.TryGetProperty("attachments", out var attachmentsElement))
                    {
                        attachments = JsonSerializer.Deserialize<List<MessageAttachmentResponse>>(attachmentsElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }
                }
                catch { /* Bỏ qua lỗi parse */ }
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
                Content = messageData.Content,
                CreatedAt = messageData.CreatedAt.ToUtc(),
                Attachments = attachments,
                ReplyToMessageId = messageData.ReplyToMessageId
            };
        }

        public async Task<ConversationResponse> GetConversationByIdAsync(string currentUserId, string conversationId, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(currentUserId))
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");

            if (string.IsNullOrWhiteSpace(conversationId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Conversation ID is required.");

            // KẾT NỐI VÀ LẤY DỮ LIỆU CƠ BẢN
            var query = _context.ConversationParticipants.AsNoTracking()
                .Where(cp => cp.ConversationId == conversationId && cp.UserId == currentUserId && !cp.IsKicked)
                .Join(_context.Conversations.AsNoTracking(),
                    cp => cp.ConversationId,
                    c => c.Id,
                    (cp, c) => new { Participant = cp, Conversation = c });

            var rawData = await query.Select(x => new
            {
                x.Conversation.Id,
                x.Conversation.Type,
                x.Conversation.Name,
                x.Conversation.AvatarUrl,
                x.Conversation.CreatedAt,
                x.Conversation.LastMessageAt,
                x.Participant.LastReadAt,
                LastMessage = _context.Messages
                    .Where(m => m.ConversationId == x.Conversation.Id)
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => new
                    {
                        m.Id,
                        m.SenderId,
                        m.Type,
                        m.Content,
                        m.CreatedAt,
                        m.Metadata
                    })
                    .FirstOrDefault(),
                OtherParticipant = x.Conversation.Type == "Direct"
                    ? _context.ConversationParticipants
                        .Where(p => p.ConversationId == x.Conversation.Id && p.UserId != currentUserId)
                        .Select(p => new { p.UserId, p.Role, p.LastReadMessageId })
                        .FirstOrDefault()
                    : null
            }).FirstOrDefaultAsync(cancel);

            if (rawData == null)
                throw new NotFoundException(ErrorCodes.CONVERSATION.NOT_FOUND, "Conversation not found or you are not an active member.");

            // DATA GATHERING (LẤY INFO USER & ONLINE STATUS)
            var userIdsToFetch = new HashSet<string>();
            var groupMembers = new List<(string UserId, string Role, string LastReadMessageId)>();

            if (rawData.Type == "Direct" && rawData.OtherParticipant != null)
                userIdsToFetch.Add(rawData.OtherParticipant.UserId);

            if (rawData.LastMessage != null)
                userIdsToFetch.Add(rawData.LastMessage.SenderId);

            if (rawData.Type == "Group")
            {
                // Lấy 3 members để hiển thị Avatar xếp chồng (như GetConversations)
                var participants = await _context.ConversationParticipants.AsNoTracking()
                    .Where(p => p.ConversationId == conversationId && !p.IsKicked)
                    .Select(p => new { p.UserId, p.Role, p.LastReadMessageId })
                    .Take(3)
                    .ToListAsync(cancel);

                foreach (var p in participants)
                {
                    groupMembers.Add((p.UserId, p.Role, p.LastReadMessageId));
                    userIdsToFetch.Add(p.UserId);
                }
            }

            var usersDict = new Dictionary<string, UserResponse>();
            if (userIdsToFetch.Any())
            {
                usersDict = await _context.Users.AsNoTracking()
                    .Where(u => userIdsToFetch.Contains(u.Id))
                    .Select(u => new UserResponse
                    {
                        Id = u.Id,
                        DisplayName = u.DisplayName ?? u.Username,
                        Username = u.Username,
                        AvatarUrl = _context.UserMedias
                            .Where(m => m.UserId == u.Id && m.IsPrimary == true && m.MediaType == UserMediaType.Avatar.GetDescription())
                            .Select(m => m.MediaUrl)
                            .FirstOrDefault()
                    })
                    .ToDictionaryAsync(u => u.Id, u => u, cancel);
            }

            var onlineStatuses = await _redisService.GetUsersOnlineStatusAsync(userIdsToFetch);

            // MAPPING DỮ LIỆU
            string? displayName = rawData.Name;
            string? avatarUrl = rawData.AvatarUrl;
            bool isOnline = false;
            var participantResponses = new List<ParticipantResponse>();

            if (rawData.Type == "Direct" && rawData.OtherParticipant != null && usersDict.TryGetValue(rawData.OtherParticipant.UserId, out var otherUser))
            {
                displayName = otherUser.DisplayName;
                avatarUrl = otherUser.AvatarUrl;
                isOnline = onlineStatuses.TryGetValue(otherUser.Id, out var status) && status;

                participantResponses.Add(new ParticipantResponse
                {
                    Id = otherUser.Id,
                    DisplayName = otherUser.DisplayName,
                    Username = otherUser.Username,
                    AvatarUrl = otherUser.AvatarUrl,
                    Role = rawData.OtherParticipant.Role,
                    IsOnline = isOnline,
                    LastReadMessageId = rawData.OtherParticipant.LastReadMessageId
                });
            }
            else if (rawData.Type == "Group")
            {
                foreach (var m in groupMembers)
                {
                    if (usersDict.TryGetValue(m.UserId, out var memberInfo))
                    {
                        participantResponses.Add(new ParticipantResponse
                        {
                            Id = memberInfo.Id,
                            DisplayName = memberInfo.DisplayName,
                            Username = memberInfo.Username,
                            AvatarUrl = memberInfo.AvatarUrl,
                            Role = m.Role,
                            LastReadMessageId = m.LastReadMessageId,
                            IsOnline = onlineStatuses.TryGetValue(memberInfo.Id, out var status) && status
                        });
                    }
                }
            }

            MessageSnippetResponse? lastMessageResponse = null;
            if (rawData.LastMessage != null)
            {
                SystemMessageMetadata? metadata = null;
                if (rawData.LastMessage.Type == "System" && !string.IsNullOrWhiteSpace(rawData.LastMessage.Metadata))
                {
                    try { metadata = JsonSerializer.Deserialize<SystemMessageMetadata>(rawData.LastMessage.Metadata, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
                    catch { }
                }

                lastMessageResponse = new MessageSnippetResponse
                {
                    Id = rawData.LastMessage.Id,
                    SenderId = rawData.LastMessage.SenderId,
                    SenderName = usersDict.TryGetValue(rawData.LastMessage.SenderId, out var sender) ? sender.DisplayName : "Unknown User",
                    Type = rawData.LastMessage.Type,
                    Content = rawData.LastMessage.Content,
                    CreatedAt = rawData.LastMessage.CreatedAt,
                    ActionMetadata = metadata
                };
            }

            return new ConversationResponse
            {
                Id = rawData.Id,
                Type = rawData.Type,
                Name = displayName,
                AvatarUrl = avatarUrl,
                CreatedAt = rawData.CreatedAt.ToUtc(),
                LastMessageAt = rawData.LastMessageAt.ToUtc(),
                HasUnread = !rawData.LastReadAt.HasValue || rawData.LastReadAt.Value == DateTime.MinValue || rawData.LastMessageAt > rawData.LastReadAt.Value,
                LastMessage = lastMessageResponse,
                Participants = participantResponses,
            };
        }
    }
}
