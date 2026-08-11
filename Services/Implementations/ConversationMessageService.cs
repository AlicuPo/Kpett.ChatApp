using Kpett.ChatApp.Data;
using Kpett.ChatApp.Constants;
using Kpett.ChatApp.DTOs.Payload.Cursor;
using Kpett.ChatApp.DTOs.Request.Conversation;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Conversation;
using Kpett.ChatApp.DTOs.Response.Conversation.Metadata;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Extensions;
using Kpett.ChatApp.Helpers;
using Kpett.ChatApp.Hubs;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Abstractions;
using Kpett.ChatApp.Events.Conversation;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Kpett.ChatApp.Services.Implementations
{
    /// <summary>Service quản lý tin nhắn trong hội thoại: lấy, gửi, cập nhật, xoá.</summary>
    public class ConversationMessageService : IConversationMessageService
    {
        private static readonly Regex MentionTokenRegex = new(
            "<@(?<userId>[^<>\\s]+)>",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly AppDbContext _context;
        private readonly IRedisService _redisService;
        private readonly IHubContext<AppHub> _chatHubContext;
        private readonly IMediator _mediator;
        private readonly ILogger<ConversationMessageService> _logger;

        private static readonly JsonSerializerOptions _jsonCamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private static readonly JsonSerializerOptions _jsonCaseInsensitive = new() { PropertyNameCaseInsensitive = true };

        /// <summary>Khởi tạo service với các dependencies.</summary>
        public ConversationMessageService(AppDbContext dbContext, IRedisService redisService, IHubContext<AppHub> chatHubContext, IMediator mediator, ILogger<ConversationMessageService> logger)
        {
            _context = dbContext;
            _redisService = redisService;
            _chatHubContext = chatHubContext;
            _mediator = mediator;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<PaginatedData<MessageResponse>> GetMessagesAsync(string currentUserId, string conversationId, MessageListRequest request, CancellationToken cancel)
        {
            _logger.LogInformation("User {UserId} is retrieving messages for conversation {ConversationId}", currentUserId, conversationId);

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                _logger.LogWarning("Get messages rejected for conversation {ConversationId} because current user ID is empty", conversationId);
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");
            }
            if (string.IsNullOrWhiteSpace(conversationId))
            {
                _logger.LogWarning("Get messages rejected for user {UserId} because conversation ID is empty", currentUserId);
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Conversation ID is required.");
            }

            var limit = request.Limit <= 0 ? 20 : Math.Min(request.Limit, 50);
            bool isParticipant = await _context.ConversationParticipants.AnyAsync(p => p.ConversationId == conversationId && p.UserId == currentUserId && !p.IsKicked, cancel);
            if (!isParticipant)
            {
                _logger.LogWarning("User {UserId} attempted to read messages from conversation {ConversationId} without active membership", currentUserId, conversationId);
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You do not have permission to view messages in this conversation.");
            }

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
                    m.ConversationId,
                    m.SenderId,
                    m.Type,
                    m.Content,
                    m.CreatedAt,
                    m.UpdatedAt,
                    m.IsDeleted,
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

            var messageIds = rawMessages.Select(m => m.Id).ToList();
            var attachmentEntities = await _context.MessageAttachments
                .AsNoTracking()
                .Where(a => messageIds.Contains(a.MessageId))
                .OrderBy(a => a.CreatedAt)
                .Select(a => new { a.Id, a.MessageId, a.Type, a.Url, a.PublicId, a.Filename, a.FileSize, a.Width, a.Height })
                .ToListAsync(cancel);

var attachmentsByMessage = attachmentEntities
                .GroupBy(a => a.MessageId)
                .ToDictionary(g => g.Key, g => g.Select(a => new MessageAttachmentResponse
                {
                    Id = a.Id,
                    MessageId = a.MessageId,
                    Type = a.Type,
                    Url = a.Url,
                    PublicId = a.PublicId,
                    Filename = a.Filename,
                    FileSize = a.FileSize,
                    Width = a.Width,
                    Height = a.Height
                }).ToList());

            var mentionEntities = await _context.MessageMentions
                .AsNoTracking()
                .Where(m => messageIds.Contains(m.MessageId))
                .Select(m => new { m.MessageId, m.UserId, m.Username, m.DisplayName })
                .ToListAsync(cancel);

            var mentionsByMessage = mentionEntities
                .GroupBy(m => m.MessageId)
                .ToDictionary(g => g.Key, g => g.Select(m => new MessageMentionResponse
                {
                    UserId = m.UserId,
                    Username = m.Username,
                    DisplayName = m.DisplayName
                }).ToList());

            var messageResponses = rawMessages.Select(m =>
            {
                var response = new MessageResponse
                {
                    Id = m.Id,
                    ConversationId = m.ConversationId,
                    SenderId = m.SenderId,
                    SenderName = m.SenderName ?? "Unknown User",
                    SenderAvatarUrl = m.SenderAvatarUrl,
                    Type = m.Type,
                    Content = m.IsDeleted ? null : m.Content,
                    CreatedAt = m.CreatedAt.ToUtc(),
                    UpdatedAt = m.UpdatedAt?.ToUtc(),
                    IsDeleted = m.IsDeleted,
                    ClientMessageId = m.ClientMessageId,
                    ReplyToMessageId = m.ReplyToMessageId,
                    ActionMetadata = ParseSystemMetadata(m.Type, m.Metadata)
                };

                if (attachmentsByMessage.TryGetValue(m.Id, out var attachments))
                {
                    response.Attachments = attachments;
                }

                if (mentionsByMessage.TryGetValue(m.Id, out var mentions))
                {
                    response.Mentions = mentions;
                }

                return response;
            }).ToList();

            _logger.LogInformation("User {UserId} retrieved {Count} messages for conversation {ConversationId}", currentUserId, messageResponses.Count, conversationId);
            return new PaginatedData<MessageResponse> { Items = messageResponses, Pagination = new CursorPaginationMeta { NextCursor = nextCursor, Limit = limit } };
        }

        /// <inheritdoc />
        public async Task<MessageResponse> SendMessageAsync(string currentUserId, string conversationId, SendMessageRequest request, CancellationToken cancel)
        {
            _logger.LogInformation("User {UserId} is sending message to conversation {ConversationId}. Type: {MessageType}. HasAttachments: {HasAttachments}", currentUserId, conversationId, request.Type, request.Attachments?.Any() == true);

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                _logger.LogWarning("Send message rejected for conversation {ConversationId} because current user ID is empty", conversationId);
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");
            }
            if (string.IsNullOrWhiteSpace(request.ClientMessageId))
            {
                _logger.LogWarning("Send message rejected for user {UserId} in conversation {ConversationId} because ClientMessageId is empty", currentUserId, conversationId);
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "ClientMessageId is required for idempotency.");
            }
            if (string.IsNullOrWhiteSpace(request.Content) && (request.Attachments == null || !request.Attachments.Any()))
            {
                _logger.LogWarning("Send message rejected for user {UserId} in conversation {ConversationId} because message has no content or attachments", currentUserId, conversationId);
                throw new BadRequestException(ErrorCodes.VALIDATION.INVALID, "Message must contain either text content or attachments.");
            }

            var conversation = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId, cancel);
            if (conversation == null)
            {
                _logger.LogWarning("Send message rejected because conversation {ConversationId} was not found", conversationId);
                throw new NotFoundException(ErrorCodes.CONVERSATION.NOT_FOUND, "Conversation not found.");
            }

var currentUserParticipant = await _context.ConversationParticipants.FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == currentUserId && !p.IsKicked, cancel);
            if (currentUserParticipant == null)
            {
                _logger.LogWarning("User {UserId} attempted to send message to conversation {ConversationId} without active membership", currentUserId, conversationId);
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You are not a participant of this conversation.");
            }

            var mentionIds = conversation.Type == "Group" ? ExtractMentionUserIds(request.Content) : new List<string>();
            if (mentionIds.Count > 0)
            {
                await EnsureMentionTargetsAreParticipantsAsync(conversationId, mentionIds, cancel);
            }

            var existingMessageId = await _context.Messages.Where(m => m.ConversationId == conversationId && m.ClientMessageId == request.ClientMessageId).Select(m => m.Id).FirstOrDefaultAsync(cancel);
            if (existingMessageId != null)
            {
                _logger.LogInformation("Message send for user {UserId} in conversation {ConversationId} reused existing message {MessageId}", currentUserId, conversationId, existingMessageId);
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

            conversation.LastMessageAt = now;
            conversation.IsActive = true;
            currentUserParticipant.LastReadAt = now;
            currentUserParticipant.LastReadMessageId = messageId;

            _context.Conversations.Update(conversation);
            _context.ConversationParticipants.Update(currentUserParticipant);

            if (request.Attachments != null && request.Attachments.Count != 0)
            {
                var attachments = request.Attachments.Select(a => new MessageAttachment
                {
                    Id = Guid.NewGuid().ToString(),
                    MessageId = messageId,
                    Type = a.Type,
                    Url = a.Url,
                    PublicId = a.PublicId,
                    Filename = a.Filename,
                    FileSize = a.FileSize,
                    CreatedAt = now
                }).ToList();

                _context.MessageAttachments.AddRange(attachments);
            }

await _context.SaveChangesAsync(cancel);

            if (mentionIds.Count > 0)
            {
                await SyncMessageMentionsAsync(messageId, mentionIds, now, cancel);

                var snippet = (request.Content ?? string.Empty).Length > 50
                    ? request.Content!.Substring(0, 50) + "..."
                    : request.Content;

                await _mediator.Publish(new MessageMentionedEvent
                {
                    ConversationId = conversationId,
                    MessageId = messageId,
                    ActorId = currentUserId,
                    MentionedUserIds = mentionIds,
                    TextSnippet = snippet ?? string.Empty
                }, cancel);
            }

            var responseDto = await MapToMessageResponseAsync(messageId, currentUserId, cancel);
            var participantIds = await _context.ConversationParticipants.AsNoTracking().Where(p => p.ConversationId == conversationId && !p.IsKicked).Select(p => p.UserId).ToListAsync(cancel);

            try
            {
                await _chatHubContext.Clients.Users(participantIds).SendAsync("ReceiveNewMessage", responseDto, cancel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR Error SendMessageAsync");
            }

            _logger.LogInformation("User {UserId} sent message {MessageId} to conversation {ConversationId}", currentUserId, messageId, conversationId);
            return responseDto;
        }

        /// <inheritdoc />
        public async Task<MessageResponse> UpdateMessageAsync(string currentUserId, string conversationId, string messageId, UpdateMessageRequest request, CancellationToken cancel)
        {
            _logger.LogInformation("User {UserId} is updating message {MessageId} in conversation {ConversationId}", currentUserId, messageId, conversationId);

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                _logger.LogWarning("Update message rejected for conversation {ConversationId} because current user ID is empty", conversationId);
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");
            }

            if (string.IsNullOrWhiteSpace(conversationId) || string.IsNullOrWhiteSpace(messageId))
            {
                _logger.LogWarning("Update message rejected for user {UserId} because conversation ID or message ID is empty", currentUserId);
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Conversation ID and message ID are required.");
            }

            if (request == null || string.IsNullOrWhiteSpace(request.Content))
            {
                _logger.LogWarning("Update message {MessageId} rejected for user {UserId} because content is empty", messageId, currentUserId);
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Message content is required.");
            }

            var isParticipant = await _context.ConversationParticipants
                .AnyAsync(p => p.ConversationId == conversationId && p.UserId == currentUserId && !p.IsKicked, cancel);

            if (!isParticipant)
            {
                _logger.LogWarning("User {UserId} attempted to update message {MessageId} in conversation {ConversationId} without active membership", currentUserId, messageId, conversationId);
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You are not a participant of this conversation.");
            }

            var message = await _context.Messages
                .FirstOrDefaultAsync(m => m.Id == messageId && m.ConversationId == conversationId, cancel);

            if (message == null)
            {
                _logger.LogWarning("Update message rejected because message {MessageId} was not found in conversation {ConversationId}", messageId, conversationId);
                throw new NotFoundException(ErrorCodes.CONVERSATION.NOT_FOUND, "Message not found.");
            }

            if (message.SenderId != currentUserId)
            {
                _logger.LogWarning("User {UserId} attempted to update message {MessageId} owned by user {OwnerId}", currentUserId, messageId, message.SenderId);
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You can only update your own messages.");
            }

            if (message.IsDeleted || message.Type == MessageType.System.GetDescription())
            {
                _logger.LogWarning("Update message {MessageId} rejected because message is deleted or system type", messageId);
                throw new BadRequestException(ErrorCodes.CONVERSATION.INVALID_MESSAGE, "This message cannot be updated.");
            }

message.Content = request.Content.Trim();
            message.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancel);

            var conversationType = await _context.Conversations.AsNoTracking().Where(c => c.Id == conversationId).Select(c => c.Type).FirstOrDefaultAsync(cancel);
            var mentionIds = conversationType == "Group" ? ExtractMentionUserIds(message.Content) : new List<string>();
            if (mentionIds.Count > 0)
            {
                await EnsureMentionTargetsAreParticipantsAsync(conversationId, mentionIds, cancel);
                await SyncMessageMentionsAsync(message.Id, mentionIds, message.UpdatedAt.Value, cancel);
                await _context.SaveChangesAsync(cancel);
            }
            else
            {
                var existingMentions = await _context.MessageMentions.Where(m => m.MessageId == message.Id).ToListAsync(cancel);
                if (existingMentions.Count > 0)
                {
                    _context.MessageMentions.RemoveRange(existingMentions);
                    await _context.SaveChangesAsync(cancel);
                }
            }

            var response = await MapToMessageResponseAsync(message.Id, currentUserId, cancel);
            var participantIds = await _context.ConversationParticipants.AsNoTracking()
                .Where(p => p.ConversationId == conversationId && !p.IsKicked)
                .Select(p => p.UserId)
                .ToListAsync(cancel);

            try
            {
                await _chatHubContext.Clients.Users(participantIds).SendAsync("MessageUpdated", response, cancel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR Error UpdateMessageAsync");
            }

            _logger.LogInformation("User {UserId} updated message {MessageId} in conversation {ConversationId}", currentUserId, messageId, conversationId);
            return response;
        }

        /// <inheritdoc />
        public async Task DeleteMessageAsync(string currentUserId, string conversationId, string messageId, CancellationToken cancel)
        {
            _logger.LogInformation("User {UserId} is deleting message {MessageId} in conversation {ConversationId}", currentUserId, messageId, conversationId);

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                _logger.LogWarning("Delete message rejected for conversation {ConversationId} because current user ID is empty", conversationId);
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");
            }

            if (string.IsNullOrWhiteSpace(conversationId) || string.IsNullOrWhiteSpace(messageId))
            {
                _logger.LogWarning("Delete message rejected for user {UserId} because conversation ID or message ID is empty", currentUserId);
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Conversation ID and message ID are required.");
            }

            var isParticipant = await _context.ConversationParticipants
                .AnyAsync(p => p.ConversationId == conversationId && p.UserId == currentUserId && !p.IsKicked, cancel);

            if (!isParticipant)
            {
                _logger.LogWarning("User {UserId} attempted to delete message {MessageId} in conversation {ConversationId} without active membership", currentUserId, messageId, conversationId);
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You are not a participant of this conversation.");
            }

            var message = await _context.Messages
                .FirstOrDefaultAsync(m => m.Id == messageId && m.ConversationId == conversationId, cancel);

            if (message == null)
            {
                _logger.LogWarning("Delete message rejected because message {MessageId} was not found in conversation {ConversationId}", messageId, conversationId);
                throw new NotFoundException(ErrorCodes.CONVERSATION.NOT_FOUND, "Message not found.");
            }

            if (message.SenderId != currentUserId)
            {
                _logger.LogWarning("User {UserId} attempted to delete message {MessageId} owned by user {OwnerId}", currentUserId, messageId, message.SenderId);
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You can only delete your own messages.");
            }

            if (message.Type == MessageType.System.GetDescription())
            {
                _logger.LogWarning("Delete message {MessageId} rejected because system messages cannot be deleted", messageId);
                throw new BadRequestException(ErrorCodes.CONVERSATION.INVALID_MESSAGE, "System messages cannot be deleted.");
            }

            if (!message.IsDeleted)
            {
                message.IsDeleted = true;
                message.Content = null;
                message.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancel);
            }

            var response = await MapToMessageResponseAsync(message.Id, currentUserId, cancel);
            var participantIds = await _context.ConversationParticipants.AsNoTracking()
                .Where(p => p.ConversationId == conversationId && !p.IsKicked)
                .Select(p => p.UserId)
                .ToListAsync(cancel);

            try
            {
                await _chatHubContext.Clients.Users(participantIds).SendAsync("MessageDeleted", new { conversationId, messageId }, cancel);
                await _chatHubContext.Clients.Users(participantIds).SendAsync("MessageUpdated", response, cancel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR Error DeleteMessageAsync");
            }
            _logger.LogInformation("User {UserId} deleted message {MessageId} in conversation {ConversationId}", currentUserId, messageId, conversationId);
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

var attachments = await _context.MessageAttachments
                .AsNoTracking()
                .Where(a => a.MessageId == messageId)
                .OrderBy(a => a.CreatedAt)
                .Select(a => new MessageAttachmentResponse
                {
                    Id = a.Id,
                    MessageId = a.MessageId,
                    Type = a.Type,
                    Url = a.Url,
                    PublicId = a.PublicId,
                    Filename = a.Filename,
                    FileSize = a.FileSize,
                    Width = a.Width,
                    Height = a.Height
                }).ToListAsync(cancel);

            var mentions = await _context.MessageMentions
                .AsNoTracking()
                .Where(m => m.MessageId == messageId)
                .Select(m => new MessageMentionResponse
                {
                    UserId = m.UserId,
                    Username = m.Username,
                    DisplayName = m.DisplayName
                }).ToListAsync(cancel);

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
                ActionMetadata = ParseSystemMetadata(messageData.Type, messageData.Metadata),
                Attachments = attachments,
Mentions = mentions.Count > 0 ? mentions : null
            };
        }

        private async Task EnsureMentionTargetsAreParticipantsAsync(
            string conversationId,
            IReadOnlyCollection<string> mentionUserIds,
            CancellationToken cancel)
        {
            if (mentionUserIds.Count == 0)
            {
                return;
            }

            var activeParticipantIds = await _context.ConversationParticipants
                .AsNoTracking()
                .Where(p => p.ConversationId == conversationId && !p.IsKicked && mentionUserIds.Contains(p.UserId))
                .Select(p => p.UserId)
                .ToListAsync(cancel);

            var activeSet = activeParticipantIds.ToHashSet(StringComparer.Ordinal);
            var invalidIds = mentionUserIds.Where(id => !activeSet.Contains(id)).Distinct().ToList();

            if (invalidIds.Count > 0)
            {
                _logger.LogWarning("Message mention rejected in conversation {ConversationId} because targets are not active members: {TargetIds}", conversationId, string.Join(", ", invalidIds));
                throw new BadRequestException(
                    ErrorCodes.MENTION.NOT_IN_CONVERSATION,
                    $"You can only mention users who are in this group: {string.Join(", ", invalidIds)}");
            }
        }

        private async Task SyncMessageMentionsAsync(string messageId, IReadOnlyCollection<string> mentionUserIds, DateTime utcNow, CancellationToken cancel)
        {
            var mentionSnapshots = await _context.Users
                .AsNoTracking()
                .Where(u => mentionUserIds.Contains(u.Id))
                .Select(u => new MessageMentionSnapshot(u.Id, u.Username ?? u.DisplayName ?? u.Id, u.DisplayName))
                .ToListAsync(cancel);

            var snapshotByUserId = mentionSnapshots.ToDictionary(s => s.UserId, StringComparer.Ordinal);
            var orderedIds = mentionUserIds.Distinct().ToList();

            await _context.MessageMentions
                .Where(m => m.MessageId == messageId)
                .ExecuteDeleteAsync(cancel);

            var mentionEntities = orderedIds.Select(userId =>
            {
                var snapshot = snapshotByUserId[userId];

                return new MessageMention
                {
                    Id = Guid.NewGuid().ToString(),
                    MessageId = messageId,
                    UserId = userId,
                    Username = snapshot.Username,
                    DisplayName = snapshot.DisplayName,
                    IsNotified = false,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                };
            }).ToList();

            _context.MessageMentions.AddRange(mentionEntities);
            await _context.SaveChangesAsync(cancel);
        }

        private static List<string> ExtractMentionUserIds(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return new List<string>();
            }

            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (Match match in MentionTokenRegex.Matches(content))
            {
                var mentionUserId = match.Groups["userId"].Value.Trim();
                if (string.IsNullOrWhiteSpace(mentionUserId))
                {
                    continue;
                }

                if (seen.Add(mentionUserId))
                {
                    result.Add(mentionUserId);
                }
            }

            return result;
        }

        private sealed record MessageMentionSnapshot(string UserId, string Username, string? DisplayName);

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
    }
}
