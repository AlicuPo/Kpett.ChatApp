using Kpett.ChatApp.DTOs;
using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Receive;
using Kpett.ChatApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using Newtonsoft.Json.Linq;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Kpett.ChatApp.Respository
{
    public class MessageRespository : IMessage
    {
        private readonly AppDbContext _dbcontext;
        private readonly IToken _token;
        private readonly Kpett.ChatApp.Receive.IRealtimeService _realtime;
        private readonly INotificationService _notificationService;

        public MessageRespository(AppDbContext dbContext, IToken token, IRealtimeService realtime, INotificationService notification)
        {
            _dbcontext = dbContext;
            _token = token;
            _realtime = realtime;
            _notificationService = notification;
        }
        public async Task<MessagePageResult> GetMessagesAsync(string conversationId, string currentUserId, long? cursorMessageId, int pageSize, CancellationToken cancel)
        {
            // Base query
            var query = _dbcontext.Messages
                .AsNoTracking()
                .Where(x => x.ConversationId == conversationId);

            // Cursor pagination
            if (cursorMessageId.HasValue)
            {
                query = query.Where(x => x.Id < cursorMessageId.Value);
            }

            // Load messages + details
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
                throw new AppException(StatusCodes.Status404NotFound, "User is not a participant of this conversation.");
            }

            if (request.LastReadMessageId > (participant.LastReadMessageId ?? 0))
            {
                bool messageExists = await _dbcontext.Messages
                    .AnyAsync(m => m.Id == request.LastReadMessageId && m.ConversationId == id, cancel);

                if (!messageExists)
                {
                    throw new AppException(StatusCodes.Status400BadRequest, "Invalid Message ID for this conversation.");
                }
                participant.LastReadMessageId = request.LastReadMessageId;

                await _dbcontext.SaveChangesAsync(cancel);
            }
        }


        public async Task SendMessageAsync(string conversationId, string senderId, SendMessageRequest request, CancellationToken cancel)
        {

            if (conversationId == null)
                throw new AppException(StatusCodes.Status400BadRequest, "Conversation ID cannot be null.");

            if (senderId == null)
                throw new AppException(StatusCodes.Status400BadRequest, "Sender ID cannot be null.");

                var findConversation = await _dbcontext.Conversations.Where(_ => _.Id == conversationId).FirstOrDefaultAsync();
           
            if(findConversation == null)
               throw new AppException(StatusCodes.Status400BadRequest,"Conversation not found");
            var _id = Guid.NewGuid().ToString();
            var message = new Message
            {
                ConversationId = conversationId,
                SenderId = senderId,
                ClientMessageId = request.ClientMessageId,
                Metadata = request.Metadata,
                Type = request.Type,
                CreatedAt = DateTime.UtcNow
            };
            _dbcontext.Messages.Add(message);
            await _dbcontext.SaveChangesAsync(cancel);

            var messageDetail = new MessageDetail
            {
                MessageId = message.Id,
                Content = request.Content,
                Color = request.Coler
            };
            _dbcontext.MessageDetails.Add(messageDetail);

            

            await _dbcontext.SaveChangesAsync(cancel);


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
