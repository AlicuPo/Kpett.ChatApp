using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Request.Message;
using Kpett.ChatApp.DTOs.Response.Message;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Receive;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Kpett.ChatApp.Services.Impls
{
    public class MessageService : IMessageService
    {
        private readonly AppDbContext _dbcontext;
        private readonly IRealtimeService _realtime;
        private readonly INotificationService _notificationService;
        private readonly IConversationAccessService _conversationAccessService;

        public MessageService(
            AppDbContext dbContext,
            IRealtimeService realtime,
            INotificationService notification,
            IConversationAccessService conversationAccessService)
        {
            _dbcontext = dbContext;
            _realtime = realtime;
            _notificationService = notification;
            _conversationAccessService = conversationAccessService;
        }

        public async Task<MessagePageResult> GetMessagesAsync(string conversationId, string currentUserId, long? cursorMessageId, int pageSize, CancellationToken cancel)
        {
            await _conversationAccessService.EnsureCanAccessConversationAsync(conversationId, currentUserId, cancel);

            var query = _dbcontext.Messages
                .AsNoTracking()
                .Where(x => x.ConversationId == conversationId);

            if (cursorMessageId.HasValue)
            {
                query = query.Where(x => x.Id < cursorMessageId.Value);
            }

            var messages = await (
                from m in query
                join d in _dbcontext.MessageDetails on m.Id equals d.MessageId
                orderby m.Id descending
                select new MessageDTO
                {
                    Id = m.Id,
                    SenderId = m.SenderId,
                    Type = m.Type,
                    Content = d.Content,
                    Metadata = m.Metadata,
                    CreatedAt = m.CreatedAt
                })
                .Take(pageSize)
                .ToListAsync(cancel);

            return new MessagePageResult
            {
                Messages = messages,
                OldestMessageId = messages.Any() ? messages.Min(x => x.Id) : null,
                HasMore = messages.Count == pageSize
            };
        }

        public async Task<MessageDTO> SendMessageAsync(string conversationId, string senderId, SendMessageRequest request, CancellationToken cancel)
        {
            await _conversationAccessService.EnsureCanAccessConversationAsync(conversationId, senderId, cancel);

            if (string.IsNullOrWhiteSpace(request?.Content))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Message content cannot be empty.");

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

            _dbcontext.MessageDetails.Add(new MessageDetail
            {
                MessageId = message.Id,
                Content = request.Content,
                Color = request.Color
            });
            await _dbcontext.SaveChangesAsync(cancel);

            var messageDto = new MessageDTO
            {
                Id = message.Id,
                SenderId = senderId,
                Content = request.Content,
                Type = message.Type,
                Metadata = request.Metadata,
                CreatedAt = message.CreatedAt ?? DateTime.UtcNow
            };

            try
            {
                await _realtime.PublishToGroupAsync(
                    conversationId,
                    "ReceiveMessage",
                    new
                    {
                        conversationId,
                        message = messageDto,
                        timestamp = DateTime.UtcNow
                    });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Real-time notification failed: {ex.Message}");
            }

            try
            {
                await _notificationService.CreateMessageNotificationsAsync(conversationId, senderId, messageDto);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Notification creation failed: {ex.Message}");
            }

            return messageDto;
        }

        public async Task MarkAsReadAsync(string conversationId, string currentUserId, ReadMessageRequest request, CancellationToken cancel)
        {
            if (request == null)
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Read message request is required.");

            await _conversationAccessService.EnsureCanAccessConversationAsync(conversationId, currentUserId, cancel);

            var participant = await _dbcontext.ConversationParticipants
                .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == currentUserId, cancel);

            if (participant == null)
                throw new ForbiddenException(ErrorCodes.CONVERSATION.USER_NOT_IN_CONVERSATION, "User is not a participant of this conversation.");

            if (request.LastReadMessageId <= (participant.LastReadMessageId ?? 0))
            {
                participant.LastReadAt = request.ReadAt ?? DateTime.UtcNow;
                await _dbcontext.SaveChangesAsync(cancel);
                return;
            }

            var messageExists = await _dbcontext.Messages
                .AnyAsync(m => m.Id == request.LastReadMessageId && m.ConversationId == conversationId, cancel);

            if (!messageExists)
                throw new ConflictException(ErrorCodes.CONVERSATION.INVALID_MESSAGE, "Invalid Message ID for this conversation.");

            participant.LastReadMessageId = request.LastReadMessageId;
            participant.LastReadAt = request.ReadAt ?? DateTime.UtcNow;
            await _dbcontext.SaveChangesAsync(cancel);
        }
    }
}
