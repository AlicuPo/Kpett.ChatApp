using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Kpett.ChatApp.Respository
{
    public class MessageRespository : IMessage
    {
        private readonly AppDbContext _dbcontext;

        public MessageRespository(AppDbContext dbContext)
        {
            _dbcontext = dbContext;
        }
        public async Task<MessageRespone> GetMessages(MessageRequest message, SearchRequest search, CancellationToken cancel)
        {
            var messageQuery = _dbcontext.Messages.Where(m => m.ConversationId == message.ConversationId).AsQueryable();

            if (message.cursorMessageId.HasValue)
            {
                messageQuery = messageQuery.Where(m => m.Id < message.cursorMessageId.Value);
            }
            var messages = await messageQuery.OrderByDescending(m => m.Id).Take(search.PageSize).ToListAsync(cancel);

            var messageDtos = messages.Select(m => new MessageDto
            {
                Id = m.Id,
                Content = m.Metadata,
                CreatedAt = m.CreatedAt,
                SenderId = m.SenderId
            }).ToList();

            var oldestMessageId = messages.Any() ? messages.Min(m => m.Id) : (long?)null;
            return new MessageRespone
            {
                Messages = messageDtos,
                OldestMessageId = oldestMessageId
            };
        }
    }
}
