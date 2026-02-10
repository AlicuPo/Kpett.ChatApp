using Kpett.ChatApp.DTOs;
using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Kpett.ChatApp.Services.Impls
{
    public class ConversationService : IConversationService
    {
        private readonly AppDbContext _dbcontext;
        private readonly IJwtService _token;

        public ConversationService(AppDbContext dbContext, IJwtService token)
        {
            _dbcontext = dbContext;
            _token = token;
        }

        public async Task<ConversationResponse> CreateConversaTion(ConversationKeysRequest request, CancellationToken cancel)
        {
            if (request == null)
                throw new AppException(StatusCodes.Status400BadRequest, "Request cannot be null");

            string _id = Guid.NewGuid().ToString();
            var newconversation = new Conversation
            {
                Id = _id,
                Name = request.Name,
                AvatarUrl = request.AvatarUrl,
                Type = request.Type,
                LastMessageAt = DateTime.UtcNow,

            };
            await _dbcontext.Conversations.AddAsync(newconversation, cancel);
            var newconversationKeys = new ConversationKey
            {
                Id = Guid.NewGuid().ToString(),
                ConversationId = newconversation.Id,
                UserLowId = request.UserLow,
                UserHighId = request.UserHigh
            };
            await _dbcontext.ConversationKeys.AddAsync(newconversationKeys, cancel);

            // Add participants
            var participantLow = new ConversationParticipant
            {
                ConversationId = newconversation.Id,
                UserId = request.UserLow,
                LastReadAt = DateTime.UtcNow
            };
            var participantHigh = new ConversationParticipant
            {
                ConversationId = newconversation.Id,
                UserId = request.UserHigh,
                LastReadAt = DateTime.UtcNow

            };

            await _dbcontext.ConversationParticipants.AddRangeAsync(participantLow, participantHigh);

            await _dbcontext.SaveChangesAsync(cancel);

            return new ConversationResponse
            {
                Id = newconversation.Id,
                Name = request.Name,
                AvatarUrl = request.AvatarUrl,
                Type = request.Type,
                LastMessageAt = newconversation.LastMessageAt,

            };
        }

        public async Task<List<ConversationResponse>> GetConversationList(SearchRequest search, CancellationToken cancel)
        {
            var userClaims = _token.GetUserClaims();
            var userIdToken = userClaims?.UserId ?? string.Empty;

            var userName = userClaims?.Username ?? string.Empty;
            if (string.IsNullOrEmpty(userName))
            {
                userName = "Unknown";
            }

            // Lấy danh sách Conversations mà User tham gia
            var query = from p in _dbcontext.ConversationParticipants.AsNoTracking()
                        where p.UserId == userIdToken && (p.IsArchived == null || p.IsArchived == false)
                        join c in _dbcontext.Conversations.AsNoTracking() on p.ConversationId equals c.Id

                        let lastMessage = _dbcontext.Messages
                            .AsNoTracking()
                            .Where(_ => _.ConversationId == c.Id)
                            .OrderByDescending(m => m.Id)
                           .FirstOrDefault()
                    
                        let unreadCount = _dbcontext.Messages
                            .AsNoTracking()
                            .Where(_ => _.ConversationId == c.Id
                                   && _.SenderId != userIdToken
                                   && _.Id > (p.LastReadMessageId ?? 0))
                            .Count()

                        orderby c.LastMessageAt descending
                        select new ConversationResponse
                        {
                            Id = c.Id,
                            Name = c.Name,
                            AvatarUrl = c.AvatarUrl,
                            Type = c.Type,
                            LastMessageAt = c.LastMessageAt,
                            UnreadCount = unreadCount,
                            LastMessage = lastMessage != null ? new LastMessageDto
                            {
                                Content = lastMessage.Metadata,
                                SenderId = lastMessage.SenderId,
                                CreatedAt = lastMessage.CreatedAt
                            } : null
                        };

            return await query
                .Skip((search.Page - 1) * search.PageSize)
                .Take(search.PageSize)
                .ToListAsync(cancel);
        }
    }
}
