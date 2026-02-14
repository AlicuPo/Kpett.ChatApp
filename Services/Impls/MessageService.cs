using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs;
using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Receive;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using Newtonsoft.Json.Linq;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Kpett.ChatApp.Services.Impls
{
    public class MessageService : IMessageService
    {
        private readonly AppDbContext _dbcontext;
        private readonly IJwtService _token;
        private readonly IRealtimeService _realtime;
        private readonly INotificationService _notificationService;

        public MessageService(AppDbContext dbContext, IJwtService token, IRealtimeService realtime, INotificationService notification)
        {
            _dbcontext = dbContext;
            _token = token;
            _realtime = realtime;
            _notificationService = notification;
        }
        public async Task<MessagePageResult> GetMessagesAsync(string conversationId, string currentUserId, long? cursorMessageId, int pageSize, CancellationToken cancel)
        {
            var query = _dbcontext.Messages.AsNoTracking().Where(x => x.ConversationId == conversationId);

            if (cursorMessageId.HasValue)
            {
                query = query.Where(x => x.Id < cursorMessageId.Value);
            }
            var messages = await (
                from m in query
                join d in _dbcontext.MessageDetails
                    on m.Id equals d.MessageId
                orderby m.Id descending
                select new MessageDTO
                {
                    Id = m.Id,
                    SenderId = m.SenderId,
                    Type = m.Type,
                    Content = d.Content,
                    Metadata = m.Metadata,
                    CreatedAt = m.CreatedAt
                }
            )
            .Take(pageSize)
            .ToListAsync(cancel);

            long? oldestMessageId = messages.Any()
                ? messages.Min(x => x.Id)
                : null;

            return new MessagePageResult
            {
                Messages = messages,
                OldestMessageId = oldestMessageId,
                HasMore = messages.Count == pageSize
            };
        }

        public async Task MarkAsRead(string id, [FromBody] ReadMessageRequest request, CancellationToken cancel)
        {
            var userClaims = _token.GetUserClaims();
            var userIdToken = userClaims?.UserId ?? string.Empty;

            var participant = await _dbcontext.ConversationParticipants
                .FirstOrDefaultAsync(p => p.ConversationId == id && p.UserId == userIdToken, cancel);

            if (participant == null)
            {
                throw new ConflictException(ErrorCodes.CONVERSATION.USER_NOT_IN_CONVERSATION, "User is not a participant of this conversation.");
            }

            if (request.LastReadMessageId > (participant.LastReadMessageId ?? 0))
            {
                bool messageExists = await _dbcontext.Messages
                    .AnyAsync(m => m.Id == request.LastReadMessageId && m.ConversationId == id, cancel);

                if (!messageExists)
                {
                    throw new ConflictException(ErrorCodes.CONVERSATION.INVALID_MESSAGE, "Invalid Message ID for this conversation.");
                }
                participant.LastReadMessageId = request.LastReadMessageId;

                await _dbcontext.SaveChangesAsync(cancel);
            }
        }


        public async Task SendMessageAsync(string conversationId, string senderId, SendMessageRequest request, CancellationToken cancel)
        {
            
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Conversation ID cannot be null or empty.");

            if (string.IsNullOrWhiteSpace(senderId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Sender ID cannot be null or empty.");

            if (string.IsNullOrWhiteSpace(request?.Content))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Message content cannot be empty.");


            var conversation = await _dbcontext.Conversations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conversationId, cancel);

            if (conversation == null)
                throw new NotFoundException(ErrorCodes.CONVERSATION.NOT_FOUND, "Conversation not found.");

            var isParticipant = await _dbcontext.ConversationParticipants
                .AsNoTracking()
                .AnyAsync(p => p.ConversationId == conversationId && p.UserId == senderId, cancel);

            if (!isParticipant)
                throw new ConflictException(ErrorCodes.CONVERSATION.USER_NOT_IN_CONVERSATION, "User is not a participant of this conversation.");

            var message = new Message
            {
                ConversationId = conversationId,
                SenderId = senderId,
                ClientMessageId = request.ClientMessageId,
                Metadata = request.Metadata,
                Type = request.Type ?? "text",
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            _dbcontext.Messages.Add(message);
            await _dbcontext.SaveChangesAsync(cancel);

            var messageDetail = new MessageDetail
            {
                MessageId = message.Id,
                Content = request.Content,
                Color = request.Color
            };

            _dbcontext.MessageDetails.Add(messageDetail);
            await _dbcontext.SaveChangesAsync(cancel);

            // Create message DTO for notifications
            var messageDTO = new MessageDTO
            {
                Id = message.Id,
                SenderId = senderId,
                Content = request.Content,
                Type = message.Type,
                Metadata = request.Metadata,
                CreatedAt = message.CreatedAt ?? DateTime.UtcNow
            };

            // Send real-time notification to all participants
            try
            {
                await _realtime.PublishToGroupAsync(
                    conversationId,
                    "ReceiveMessage",
                    new
                    {
                        conversationId = conversationId,
                        message = messageDTO,
                        timestamp = DateTime.UtcNow
                    });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Real-time notification failed: {ex.Message}");
            }

            try
            {
                await _notificationService.CreateMessageNotificationsAsync(conversationId, senderId, messageDTO);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Notification creation failed: {ex.Message}");
            }
        }


        public async Task<PagedResult<MessageDTO>> GetMessagesAsync(string conversationId, long? cursorMessageId, int limit, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            var query = _dbcontext.Messages
                .Where(x => x.ConversationId == conversationId);

            if (cursorMessageId.HasValue)
            {
                query = query.Where(x => x.Id < cursorMessageId.Value);
            }

            var messages = await query
                .OrderByDescending(x => x.Id)
                .Take(limit)
                .Select(x => new MessageDTO
                {
                    Id = x.Id,
                    Content = x.Metadata,
                    CreatedAt = x.CreatedAt,
                    SenderId = x.SenderId
                })
                .ToListAsync();

            var nextCursor = messages.Any()
                ? messages.Min(x => x.Id)
                : (long?)null;

            return new PagedResult<MessageDTO>
            {
                Items = messages,
                NextCursor = nextCursor,
                HasMore = messages.Count == limit
            };
        }


        public async Task MarkAsReadAsync(string conversationId, string userId, long lastReadMessageId, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            var participant = await _dbcontext.ConversationParticipants
                .FirstAsync(x =>
                    x.ConversationId == conversationId &&
                    x.UserId == userId);

            participant.LastReadMessageId = lastReadMessageId;
            participant.LastReadAt = DateTime.UtcNow;

            await _dbcontext.SaveChangesAsync(cancel);

            //// publish realtime
            //await _realtime.PublishAsync(
            //    $"conversation:{conversationId}",
            //    new
            //    {
            //        type = "MESSAGE_READ",
            //        conversationId,
            //        userId,
            //        lastReadMessageId,
            //        lastReadAt = participant.LastReadAt
            //    });
        }


    }
}
