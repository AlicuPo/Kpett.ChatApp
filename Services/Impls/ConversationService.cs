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
    public class ConversationService : IConversationService
    {
        private readonly AppDbContext _context;
        private readonly IRedisService _redisService;
        private readonly IHubContext<AppHub> _chatHubContext;
        private readonly ILogger<ConversationService> _logger;

        private static readonly JsonSerializerOptions _jsonCamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private static readonly JsonSerializerOptions _jsonCaseInsensitive = new() { PropertyNameCaseInsensitive = true };

        public ConversationService(AppDbContext dbContext, IRedisService redisService, IHubContext<AppHub> chatHubContext, ILogger<ConversationService> logger)
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
            {
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");
            }

            if (request == null || string.IsNullOrWhiteSpace(request.Type))
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Conversation request and type are required.");
            }

            var isGroup = request.Type.Equals("Group", StringComparison.OrdinalIgnoreCase);
            var isDirect = request.Type.Equals("Direct", StringComparison.OrdinalIgnoreCase);

            if (!isGroup && !isDirect)
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.INVALID, "Invalid conversation type. Must be 'Direct' or 'Group'.");
            }

            var uniqueParticipantIds = new HashSet<string>(request.ParticipantIds.Where(id => !string.IsNullOrWhiteSpace(id))) { currentUserId };

            // 2. VALIDATE LOGIC THEO TỪNG LOẠI
            if (isDirect)
            {
                if (uniqueParticipantIds.Count != 2)
                {
                    throw new BadRequestException(ErrorCodes.VALIDATION.INVALID, "Direct conversation must have exactly 2 different participants.");
                }
                if (string.IsNullOrWhiteSpace(request.InitialMessage))
                {
                    throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Initial message content is required to create a direct conversation.");
                }
            }
            else if (isGroup)
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Group name is required.");
                }
                if (uniqueParticipantIds.Count < 2)
                {
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

            await _context.SaveChangesAsync(cancel);

            // 7. MAP RESPONSE
            SystemMessageMetadata? actionMetadata = ParseSystemMetadata(initialMessage.Type, initialMessage.Metadata);
            var onlineStatuses = await _redisService.GetUsersOnlineStatusAsync(uniqueParticipantIds);

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
                Participants = participants.Select(p => MapParticipant(usersInfo[p.UserId], p.Role, p.LastReadMessageId, onlineStatuses.GetValueOrDefault(p.UserId))).ToList()
            };

            // [SIGNALR PUSH] Bắn bất đồng bộ
            _ = Task.Run(async () =>
            {
                try
                {
                    foreach (var userId in uniqueParticipantIds)
                    {
                        await _chatHubContext.Clients.User(userId).SendAsync("NewConversationCreated", newConversationResponse);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error pushing NewConversationCreated");
                }
            });

            return newConversationResponse;
        }

        public async Task<PaginatedData<ConversationResponse>> GetConversationsAsync(string currentUserId, ConversationListRequest request, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
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
                    participants.Add(MapParticipant(otherUser, c.OtherParticipant.Role, c.OtherParticipant.LastReadMessageId, onlineStatuses.GetValueOrDefault(otherUser.Id)));
                }
                else if (!isDirect && groupMembersDict.TryGetValue(c.Id, out var members))
                {
                    participants.AddRange(members.Where(m => usersDict.ContainsKey(m.UserId))
                        .Select(m => MapParticipant(usersDict[m.UserId], m.Role, m.LastReadMessageId, onlineStatuses.GetValueOrDefault(m.UserId))));
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
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");
            }
            if (request.UserIdsToAdd == null || !request.UserIdsToAdd.Any()) throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "User IDs to add are required.");

            var uniqueUserIdsToAdd = request.UserIdsToAdd.Where(id => !string.IsNullOrWhiteSpace(id) && id != currentUserId).Distinct().ToList();
            if (!uniqueUserIdsToAdd.Any())
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.INVALID, "No valid users to add.");
            }

            // Lấy thực thể Conversation và kiểm tra
            var conversation = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == request.ConversationId && c.Type == "Group", cancel);
            if (conversation == null) throw new NotFoundException(ErrorCodes.CONVERSATION.NOT_FOUND, "Group conversation not found.");

            // Lấy thực thể Participant của Current User
            var currentUserParticipant = await _context.ConversationParticipants.FirstOrDefaultAsync(cp => cp.ConversationId == request.ConversationId && cp.UserId == currentUserId && !cp.IsKicked, cancel);
            if (currentUserParticipant == null) throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You are not an active member of this group.");

            var existingParticipants = await _context.ConversationParticipants
                .Where(cp => cp.ConversationId == request.ConversationId && uniqueUserIdsToAdd.Contains(cp.UserId))
                .ToListAsync(cancel);

            var activeMemberIds = existingParticipants.Where(cp => !cp.IsKicked).Select(cp => cp.UserId).ToHashSet();
            var userIdsToProcess = uniqueUserIdsToAdd.Where(id => !activeMemberIds.Contains(id)).ToList();

            if (!userIdsToProcess.Any())
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.INVALID, "All provided users are already active members in the group.");
            }

            var usersDictionary = await _context.Users.AsNoTracking()
            .Where(u => userIdsToProcess.Contains(u.Id) || u.Id == currentUserId)
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName ?? u.Username, cancel);

            if (usersDictionary.Count(x => userIdsToProcess.Contains(x.Key)) != userIdsToProcess.Count)
            {
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "One or more users to add do not exist.");
            }

            var now = DateTime.UtcNow;
            string memberRole = ConversationRole.Member.GetDescription();

            // Tái kích hoạt những người đã từng bị kick
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

            // Cập nhật trạng thái (Sử dụng cách thức truyền thống Update)
            conversation.LastMessageAt = now;
            currentUserParticipant.LastReadMessageId = systemMessage.Id;
            currentUserParticipant.LastReadAt = now;

            _context.Conversations.Update(conversation);
            _context.ConversationParticipants.Update(currentUserParticipant);

            await _context.SaveChangesAsync(cancel);

            var activeIds = await _context.ConversationParticipants.AsNoTracking().Where(p => p.ConversationId == request.ConversationId && !p.IsKicked).Select(p => p.UserId).ToListAsync(cancel);
            var msgResponse = await MapToMessageResponseAsync(systemMessage.Id, currentUserId, cancel);

            _ = Task.Run(async () =>
            {
                try
                {
                    await _chatHubContext.Clients.Users(activeIds).SendAsync("ReceiveNewMessage", msgResponse);
                    await _chatHubContext.Clients.Users(userIdsToProcess).SendAsync("AddedToConversation", request.ConversationId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SignalR Error AddMembersToGroupAsync");
                }
            });

            return true;
        }

        public async Task<bool> RemoveMemberFromGroupAsync(string currentUserId, string conversationId, string userIdToRemove, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(currentUserId) || string.IsNullOrWhiteSpace(userIdToRemove))
            {
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "Invalid user info.");
            }

            var conversation = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId && c.Type == "Group", cancel);
            if (conversation == null)
            {
                throw new NotFoundException(ErrorCodes.CONVERSATION.NOT_FOUND, "Group conversation not found.");
            }

            var participants = await _context.ConversationParticipants
            .Where(cp => cp.ConversationId == conversationId && (cp.UserId == currentUserId || cp.UserId == userIdToRemove))
            .ToListAsync(cancel);

            var currentUserParticipant = participants.FirstOrDefault(cp => cp.UserId == currentUserId);
            if (currentUserParticipant == null)
            {
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You are not a member of this group.");
            }

            var targetParticipant = participants.FirstOrDefault(cp => cp.UserId == userIdToRemove);
            if (targetParticipant == null)
            {
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

            // Bắn SignalR
            var activeIds = await _context.ConversationParticipants.AsNoTracking().Where(p => p.ConversationId == conversationId && !p.IsKicked).Select(p => p.UserId).ToListAsync(cancel);
            var msgResponse = await MapToMessageResponseAsync(systemMessage.Id, currentUserId, cancel);

            _ = Task.Run(async () =>
            {
                try
                {
                    await _chatHubContext.Clients.User(userIdToRemove).SendAsync("RemovedFromConversation", conversationId);
                    await _chatHubContext.Clients.Users(activeIds).SendAsync("ReceiveNewMessage", msgResponse);
                }
                catch (Exception ex) { _logger.LogError(ex, "SignalR Error RemoveMemberFromGroupAsync"); }
            });

            return true;
        }

        public async Task<PaginatedData<MessageResponse>> GetMessagesAsync(string currentUserId, string conversationId, MessageListRequest request, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");
            }
            if (string.IsNullOrWhiteSpace(conversationId))
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Conversation ID is required.");
            }

            var limit = request.Limit <= 0 ? 20 : Math.Min(request.Limit, 50);
            bool isParticipant = await _context.ConversationParticipants.AnyAsync(p => p.ConversationId == conversationId && p.UserId == currentUserId && !p.IsKicked, cancel);
            if (!isParticipant) throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You do not have permission to view messages in this conversation.");

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

            var baseQuery = _context.Messages.AsNoTracking().Where(m => m.ConversationId == conversationId);

            if (cursorDate.HasValue && !string.IsNullOrWhiteSpace(cursorId))
            {
                baseQuery = baseQuery.Where(m => m.CreatedAt < cursorDate.Value || (m.CreatedAt == cursorDate.Value && string.Compare(m.Id, cursorId) < 0));
            }

            var rawMessages = await baseQuery
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
                    m.ClientMessageId,
                    m.ReplyToMessageId,
                    SenderName = _context.Users.Where(u => u.Id == m.SenderId).Select(u => u.DisplayName ?? u.Username).FirstOrDefault(),
                    SenderAvatarUrl = _context.UserMedias.Where(um => um.UserId == m.SenderId && um.IsPrimary && um.MediaType == "Avatar").Select(um => um.MediaUrl).FirstOrDefault()
                }).ToListAsync(cancel);

            string? nextCursor = null;
            if (rawMessages.Count > limit)
            {
                var lastItem = rawMessages[limit - 1];
                nextCursor = CursorHelper.Encode(new MessageCursorPayload { MessageId = lastItem.Id, CreatedAt = lastItem.CreatedAt });
                rawMessages.RemoveAt(limit);
            }

            var messageResponses = rawMessages.Select(m =>
            {
                return new MessageResponse
                {
                    Id = m.Id,
                    SenderId = m.SenderId,
                    SenderName = m.SenderName ?? "Unknown User",
                    SenderAvatarUrl = m.SenderAvatarUrl,
                    Type = m.Type,
                    Content = m.Content,
                    CreatedAt = m.CreatedAt.ToUtc(),
                    ClientMessageId = m.ClientMessageId,
                    ReplyToMessageId = m.ReplyToMessageId,
                    ActionMetadata = ParseSystemMetadata(m.Type, m.Metadata)
                };
            }).ToList();

            return new PaginatedData<MessageResponse> { Items = messageResponses, Pagination = new CursorPaginationMeta { NextCursor = nextCursor, Limit = limit } };
        }

        public async Task<MessageResponse> SendMessageAsync(string currentUserId, string conversationId, SendMessageRequest request, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");
            }
            if (string.IsNullOrWhiteSpace(request.ClientMessageId))
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "ClientMessageId is required for idempotency.");
            }
            if (string.IsNullOrWhiteSpace(request.Content) && (request.Attachments == null || !request.Attachments.Any()))
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.INVALID, "Message must contain either text content or attachments.");
            }

            // Lấy Entities liên quan (Dùng cách thông thường của EF)
            var conversation = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId, cancel);
            if (conversation == null)
            {
                throw new NotFoundException(ErrorCodes.CONVERSATION.NOT_FOUND, "Conversation not found.");
            }

            var currentUserParticipant = await _context.ConversationParticipants.FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == currentUserId && !p.IsKicked, cancel);
            if (currentUserParticipant == null)
            {
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You are not a participant of this conversation.");
            }

            // Idempotency: Kiểm tra chống trùng lặp tin nhắn
            var existingMessageId = await _context.Messages.Where(m => m.ConversationId == conversationId && m.ClientMessageId == request.ClientMessageId).Select(m => m.Id).FirstOrDefaultAsync(cancel);
            if (existingMessageId != null)
            {
                return await MapToMessageResponseAsync(existingMessageId, currentUserId, cancel);
            }

            var now = DateTime.UtcNow;
            var messageId = Guid.NewGuid().ToString();

            var newMessage = new Message
            {
                Id = messageId,
                ConversationId = conversationId,
                SenderId = currentUserId,
                ClientMessageId = request.ClientMessageId,
                Type = request.Type,
                Content = request.Content,
                ReplyToMessageId = request.ReplyToMessageId,
                CreatedAt = now
            };

            _context.Messages.Add(newMessage);

            // Cập nhật Database bằng Tracker EF thông thường
            conversation.LastMessageAt = now;
            currentUserParticipant.LastReadAt = now;
            currentUserParticipant.LastReadMessageId = messageId;

            _context.Conversations.Update(conversation);
            _context.ConversationParticipants.Update(currentUserParticipant);

            await _context.SaveChangesAsync(cancel);

            var responseDto = await MapToMessageResponseAsync(messageId, currentUserId, cancel);
            var participantIds = await _context.ConversationParticipants.AsNoTracking().Where(p => p.ConversationId == conversationId && !p.IsKicked).Select(p => p.UserId).ToListAsync(cancel);

            _ = Task.Run(async () =>
            {
                try
                {
                    await _chatHubContext.Clients.Users(participantIds).SendAsync("ReceiveNewMessage", responseDto);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SignalR Error SendMessageAsync");
                }
            });

            return responseDto;
        }

        public async Task MarkAsReadAsync(string conversationId, string currentUserId, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(currentUserId) || string.IsNullOrWhiteSpace(conversationId))
            {
                return;
            }

            // Lấy ID tin nhắn mới nhất
            var latestMessageId = await _context.Messages.AsNoTracking()
                .Where(m => m.ConversationId == conversationId).OrderByDescending(m => m.CreatedAt).Select(m => m.Id).FirstOrDefaultAsync(cancel);

            if (latestMessageId != null)
            {
                var participant = await _context.ConversationParticipants
                    .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == currentUserId && !p.IsKicked, cancel);

                if (participant != null)
                {
                    // Cập nhật thông qua EF Tracking
                    participant.LastReadAt = DateTime.UtcNow;
                    participant.LastReadMessageId = latestMessageId;

                    _context.ConversationParticipants.Update(participant);
                    await _context.SaveChangesAsync(cancel);

                    var otherUserIds = await _context.ConversationParticipants.AsNoTracking()
                        .Where(p => p.ConversationId == conversationId && p.UserId != currentUserId && !p.IsKicked).Select(p => p.UserId).ToListAsync(cancel);

                    if (otherUserIds.Any())
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _chatHubContext.Clients.Users(otherUserIds).SendAsync("UserReadMessage", conversationId, currentUserId, latestMessageId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error when send UserReadMessage");
                            }
                        });
                    }
                }
            }
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
                    m.Metadata,
                    m.ReplyToMessageId,
                    SenderName = _context.Users.Where(u => u.Id == m.SenderId).Select(u => u.DisplayName ?? u.Username).FirstOrDefault(),
                    SenderAvatarUrl = _context.UserMedias.Where(um => um.UserId == m.SenderId && um.IsPrimary && um.MediaType == "Avatar").Select(um => um.MediaUrl).FirstOrDefault()
                }).FirstOrDefaultAsync(cancel);

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
                ReplyToMessageId = messageData.ReplyToMessageId,
                ActionMetadata = ParseSystemMetadata(messageData.Type, messageData.Metadata)
            };
        }

        public async Task<ConversationResponse> GetConversationByIdAsync(string currentUserId, string conversationId, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");
            }
            if (string.IsNullOrWhiteSpace(conversationId))
            {
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

            if (rawData.Type == "Direct" && rawData.OtherParticipant != null && usersDict.TryGetValue(rawData.OtherParticipant.UserId, out var otherUser))
            {
                displayName = otherUser.DisplayName; avatarUrl = otherUser.AvatarUrl;
                participantResponses.Add(MapParticipant(otherUser, rawData.OtherParticipant.Role, rawData.OtherParticipant.LastReadMessageId, onlineStatuses.GetValueOrDefault(otherUser.Id)));
            }
            else if (rawData.Type == "Group")
            {
                participantResponses.AddRange(groupMembers.Where(m => usersDict.ContainsKey(m.UserId))
                    .Select(m => MapParticipant(usersDict[m.UserId], m.Role, m.LastReadMessageId, onlineStatuses.GetValueOrDefault(m.UserId))));
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

            return new ConversationResponse
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
        }

        public async Task<ConversationResponse> GetOrCreateDirectConversationAsync(string currentUserId, string otherUserId, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(currentUserId) || string.IsNullOrWhiteSpace(otherUserId))
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "User IDs are required.");
            }
            if (currentUserId == otherUserId)
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.INVALID, "Cannot create conversation with yourself.");
            }

            bool isOrderCorrect = string.CompareOrdinal(currentUserId, otherUserId) < 0;
            var userLow = isOrderCorrect ? currentUserId : otherUserId;
            var userHigh = isOrderCorrect ? otherUserId : currentUserId;

            var existingKey = await _context.ConversationKeys.FirstOrDefaultAsync(k => k.UserLowId == userLow && k.UserHighId == userHigh && k.ConversationId != null, cancel);
            if (existingKey != null)
            {
                var existingConversation = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == existingKey.ConversationId, cancel);
                if (existingConversation != null) return await GetConversationByIdAsync(currentUserId, existingConversation.Id, cancel);
            }

            var usersInfo = await BaseUserProjectionQuery().Where(u => u.Id == currentUserId || u.Id == otherUserId).ToDictionaryAsync(u => u.Id, u => u, cancel);
            if (usersInfo.Count != 2)
            {
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

            await _context.SaveChangesAsync(cancel);

            var otherUserInfo = usersInfo[otherUserId];
            var onlineStatuses = await _redisService.GetUsersOnlineStatusAsync(new[] { otherUserId });

            return new ConversationResponse
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
                Participants = participants.Select(p => MapParticipant(usersInfo[p.UserId], p.Role, p.LastReadMessageId, p.UserId == otherUserId ? onlineStatuses.GetValueOrDefault(otherUserId) : true)).ToList()
            };
        }

        public async Task<PaginatedData<ParticipantResponse>> GetGroupMembersAsync(string currentUserId, string conversationId, CursorPaginationRequest request, CancellationToken cancel)
        {
            // Validate đầu vào cơ bản
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");
            }

            if (string.IsNullOrWhiteSpace(conversationId))
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Conversation ID is required.");
            }

            var limit = request.Limit <= 0 ? 20 : Math.Min(request.Limit, 50);

            // Kiểm tra quyền truy cập (Chỉ dùng 1 Query AnyAsync tối ưu)
            var isMember = await _context.ConversationParticipants
                .AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == currentUserId && !cp.IsKicked, cancel);

            if (!isMember)
            {
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You are not an active member of this group.");
            }

            // Giải mã Cursor lấy ID làm mốc
            string? cursorId = null;
            if (!string.IsNullOrWhiteSpace(request.Cursor))
            {
                var decoded = CursorHelper.Decode<GroupMemberCursorPayload>(request.Cursor);
                if (decoded != null)
                {
                    cursorId = decoded.ParticipantId;
                }
            }

            // Xây dựng truy vấn cơ sở
            var baseQuery = _context.ConversationParticipants.AsNoTracking()
                .Where(p => p.ConversationId == conversationId && !p.IsKicked);

            // Lọc theo Cursor: Lấy các phần tử có Id lớn hơn cursorId hiện tại (Sắp xếp tăng dần A-Z)
            if (!string.IsNullOrWhiteSpace(cursorId))
            {
                baseQuery = baseQuery.Where(p => string.Compare(p.Id, cursorId) > 0);
            }

            // Lấy dữ liệu + Dư 1 record để kiểm tra xem còn trang tiếp theo không
            var rawParticipants = await baseQuery
                .OrderBy(p => p.Id)
                .Take(limit + 1)
                .Select(p => new { p.Id, p.UserId, p.Role, p.LastReadMessageId })
                .ToListAsync(cancel);

            // Xử lý phân trang
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

            // Tối ưu: Chỉ fetch thông tin User hiển thị ra màn hình
            var userIdsToFetch = rawParticipants.Select(p => p.UserId).ToList();

            var usersDict = userIdsToFetch.Any()
                ? await BaseUserProjectionQuery()
                    .Where(u => userIdsToFetch.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u, cancel)
                : new Dictionary<string, UserResponse>();

            var onlineStatuses = await _redisService.GetUsersOnlineStatusAsync(userIdsToFetch);

            // Map dữ liệu sang chuẩn DTO trả về (Sử dụng hàm helper DRY)
            var items = rawParticipants
                .Where(p => usersDict.ContainsKey(p.UserId))
                .Select(p => MapParticipant(
                    usersDict[p.UserId],
                    p.Role,
                    p.LastReadMessageId,
                    onlineStatuses.GetValueOrDefault(p.UserId)
                ))
                .ToList();

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
        private ParticipantResponse MapParticipant(UserResponse u, string role, string? lastReadMsgId, bool isOnline)
        {
            return new ParticipantResponse
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
                Username = u.Username,
                AvatarUrl = u.AvatarUrl,
                Role = role,
                LastReadMessageId = lastReadMsgId,
                IsOnline = isOnline
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

        #endregion
    }
}