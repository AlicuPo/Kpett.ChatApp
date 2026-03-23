using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs;
using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Kpett.ChatApp.Services.Impls
{
    public class ConversationService : IConversationService
    {
        private readonly AppDbContext _dbcontext;

        public ConversationService(AppDbContext dbContext)
        {
            _dbcontext = dbContext;
        }

        public async Task<ConversationResponse> CreateConversaTion(string currentUserId, ConversationKeysRequest request, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(currentUserId))
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");

            if (request == null)
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Conversation request is required.");

            if (string.IsNullOrWhiteSpace(request.UserLow) || string.IsNullOrWhiteSpace(request.UserHigh))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Conversation participants are required.");

            if (request.UserLow == request.UserHigh)
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Conversation participants must be different users.");

            if (request.UserLow != currentUserId && request.UserHigh != currentUserId)
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "You can only create conversations that include the current user.");

            var participantIds = new[] { request.UserLow, request.UserHigh };
            var existingUsers = await _dbcontext.Users
                .AsNoTracking()
                .Where(u => participantIds.Contains(u.Id))
                .Select(u => u.Id)
                .ToListAsync(cancel);

            if (existingUsers.Count != 2)
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "One or more conversation participants were not found.");

            var userLow = string.CompareOrdinal(request.UserLow, request.UserHigh) < 0 ? request.UserLow : request.UserHigh;
            var userHigh = string.CompareOrdinal(request.UserLow, request.UserHigh) < 0 ? request.UserHigh : request.UserLow;

            var conversationId = Guid.NewGuid().ToString();
            var newconversation = new Conversation
            {
                Id = conversationId,
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
                UserLowId = userLow,
                UserHighId = userHigh
            };
            await _dbcontext.ConversationKeys.AddAsync(newconversationKeys, cancel);

            var participantLow = new ConversationParticipant
            {
                Id = Guid.NewGuid().ToString(),
                ConversationId = newconversation.Id,
                UserId = userLow,
                JoinedAt = DateTime.UtcNow,
                LastReadAt = DateTime.UtcNow
            };
            var participantHigh = new ConversationParticipant
            {
                Id = Guid.NewGuid().ToString(),
                ConversationId = newconversation.Id,
                UserId = userHigh,
                JoinedAt = DateTime.UtcNow,
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

        public async Task<List<ConversationResponse>> GetConversationList(string currentUserId, SearchRequest search, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(currentUserId))
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");

            var query = from p in _dbcontext.ConversationParticipants.AsNoTracking()
                        where p.UserId == currentUserId && (p.IsArchived == null || p.IsArchived == false)
                        join c in _dbcontext.Conversations.AsNoTracking() on p.ConversationId equals c.Id

                        let lastMessage = _dbcontext.Messages
                            .AsNoTracking()
                            .Where(_ => _.ConversationId == c.Id)
                            .OrderByDescending(m => m.Id)
                            .FirstOrDefault()

                        let unreadCount = _dbcontext.Messages
                            .AsNoTracking()
                            .Where(_ => _.ConversationId == c.Id
                                   && _.SenderId != currentUserId
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
