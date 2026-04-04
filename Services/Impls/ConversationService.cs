using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Request.Conversation;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Conversation;
using Kpett.ChatApp.DTOs.Response.Message;
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

        public async Task<ConversationResponse> CreateConversationAsync(string currentUserId, ConversationKeysRequest request, CancellationToken cancel)
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
            var newConversation = new Conversation
            {
                Id = conversationId,
                Name = request.Name,
                AvatarUrl = request.AvatarUrl,
                Type = request.Type,
                LastMessageAt = DateTime.UtcNow
            };

            await _dbcontext.Conversations.AddAsync(newConversation, cancel);
            await _dbcontext.ConversationKeys.AddAsync(new ConversationKey
            {
                Id = Guid.NewGuid().ToString(),
                ConversationId = newConversation.Id,
                UserLowId = userLow,
                UserHighId = userHigh
            }, cancel);

            await _dbcontext.ConversationParticipants.AddRangeAsync(
                new ConversationParticipant
                {
                    Id = Guid.NewGuid().ToString(),
                    ConversationId = newConversation.Id,
                    UserId = userLow,
                    JoinedAt = DateTime.UtcNow,
                    LastReadAt = DateTime.UtcNow
                },
                new ConversationParticipant
                {
                    Id = Guid.NewGuid().ToString(),
                    ConversationId = newConversation.Id,
                    UserId = userHigh,
                    JoinedAt = DateTime.UtcNow,
                    LastReadAt = DateTime.UtcNow
                });

            await _dbcontext.SaveChangesAsync(cancel);

            return new ConversationResponse
            {
                Id = newConversation.Id,
                Name = newConversation.Name,
                AvatarUrl = newConversation.AvatarUrl,
                Type = newConversation.Type,
                LastMessageAt = newConversation.LastMessageAt
            };
        }

        public async Task<List<ConversationResponse>> GetConversationsAsync(string currentUserId, SearchRequest search, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(currentUserId))
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");

            var query = from p in _dbcontext.ConversationParticipants.AsNoTracking()
                        where p.UserId == currentUserId && (p.IsArchived == null || p.IsArchived == false)
                        join c in _dbcontext.Conversations.AsNoTracking() on p.ConversationId equals c.Id
                        let lastMessage = _dbcontext.Messages
                            .AsNoTracking()
                            .Where(m => m.ConversationId == c.Id)
                            .OrderByDescending(m => m.Id)
                            .FirstOrDefault()
                        let unreadCount = _dbcontext.Messages
                            .AsNoTracking()
                            .Where(m => m.ConversationId == c.Id
                                && m.SenderId != currentUserId
                                && m.Id > (p.LastReadMessageId ?? 0))
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
                            LastMessage = lastMessage != null
                                ? new LastMessageDto
                                {
                                    Content = lastMessage.Metadata,
                                    SenderId = lastMessage.SenderId,
                                    CreatedAt = lastMessage.CreatedAt
                                }
                                : null
                        };

            return await query
                .Skip((search.Page - 1) * search.PageSize)
                .Take(search.PageSize)
                .ToListAsync(cancel);
        }
    }
}
