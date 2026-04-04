using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Request.Conversation;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Conversation;
using Kpett.ChatApp.DTOs.Response.Message;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Kpett.ChatApp.Services.Impls
{
    public class ConversationService : IConversationService
    {
        private static readonly string AcceptedFriendshipStatus = FriendshipsEnums.Accepted.GetDescription();
        private readonly AppDbContext _dbcontext;

        public ConversationService(AppDbContext dbContext)
        {
            _dbcontext = dbContext;
        }

        public async Task<(ConversationResponse Conversation, bool IsCreated)> CreateConversationAsync(string currentUserId, ConversationKeysRequest request, CancellationToken cancel)
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
            var isDirectConversation = IsDirectConversation(request.Type);

            if (isDirectConversation)
            {
                var existingConversation = await TryGetExistingDirectConversationAsync(userLow, userHigh, cancel);
                if (existingConversation != null)
                {
                    return (MapConversationResponse(existingConversation), false);
                }

                await EnsureDirectParticipantsAreFriendsAsync(userLow, userHigh, cancel);
            }

            var utcNow = DateTime.UtcNow;
            var conversationId = Guid.NewGuid().ToString();
            var newConversation = new Conversation
            {
                Id = conversationId,
                Name = request.Name,
                AvatarUrl = request.AvatarUrl,
                Type = request.Type,
                CreatedByUserId = currentUserId,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
                LastMessageAt = utcNow,
                IsActive = true
            };

            await _dbcontext.Conversations.AddAsync(newConversation, cancel);

            if (isDirectConversation)
            {
                await _dbcontext.ConversationKeys.AddAsync(new ConversationKey
                {
                    Id = Guid.NewGuid().ToString(),
                    ConversationId = newConversation.Id,
                    UserLowId = userLow,
                    UserHighId = userHigh
                }, cancel);
            }

            await _dbcontext.ConversationParticipants.AddRangeAsync(
                new ConversationParticipant
                {
                    Id = Guid.NewGuid().ToString(),
                    ConversationId = newConversation.Id,
                    UserId = userLow,
                    JoinedAt = utcNow,
                    LastReadAt = utcNow
                },
                new ConversationParticipant
                {
                    Id = Guid.NewGuid().ToString(),
                    ConversationId = newConversation.Id,
                    UserId = userHigh,
                    JoinedAt = utcNow,
                    LastReadAt = utcNow
                });

            try
            {
                await _dbcontext.SaveChangesAsync(cancel);
            }
            catch (DbUpdateException) when (isDirectConversation)
            {
                _dbcontext.ChangeTracker.Clear();

                var existingConversation = await TryGetExistingDirectConversationAsync(userLow, userHigh, cancel);
                if (existingConversation != null)
                {
                    return (MapConversationResponse(existingConversation), false);
                }

                throw;
            }

            return (MapConversationResponse(newConversation), true);
        }

        public async Task<List<ConversationResponse>> GetConversationsAsync(string currentUserId, SearchRequest search, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(currentUserId))
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");

            var page = search?.Page > 0 ? search.Page : 1;
            var pageSize = search?.PageSize > 0 ? search.PageSize : 40;
            var normalizedSearch = search?.Search?.Trim();

            var conversations = _dbcontext.Conversations.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                conversations = conversations.Where(c => c.Name != null && c.Name.Contains(normalizedSearch));
            }

            var latestMessageIds = _dbcontext.Messages
                .AsNoTracking()
                .GroupBy(m => m.ConversationId)
                .Select(g => new
                {
                    ConversationId = g.Key,
                    MessageId = g.Max(m => m.Id)
                });

            var latestMessages =
                from latest in latestMessageIds
                join message in _dbcontext.Messages.AsNoTracking() on latest.MessageId equals message.Id
                join detail in _dbcontext.MessageDetails.AsNoTracking() on message.Id equals detail.MessageId into detailGroup
                from detail in detailGroup.DefaultIfEmpty()
                select new
                {
                    latest.ConversationId,
                    Content = detail != null ? detail.Content : null,
                    message.SenderId,
                    message.CreatedAt
                };

            var query =
                from p in _dbcontext.ConversationParticipants.AsNoTracking()
                where p.UserId == currentUserId && (p.IsArchived == null || p.IsArchived == false)
                join c in conversations on p.ConversationId equals c.Id
                join latestMessage in latestMessages on c.Id equals latestMessage.ConversationId into latestMessageGroup
                from latestMessage in latestMessageGroup.DefaultIfEmpty()
                let unreadCount = _dbcontext.Messages
                    .AsNoTracking()
                    .Where(m => m.ConversationId == c.Id
                        && m.SenderId != currentUserId
                        && m.Id > (p.LastReadMessageId ?? 0))
                    .Count()
                orderby c.LastMessageAt descending, c.Id descending
                select new ConversationResponse
                {
                    Id = c.Id,
                    Name = c.Name,
                    AvatarUrl = c.AvatarUrl,
                    Type = c.Type,
                    LastMessageAt = c.LastMessageAt,
                    UnreadCount = unreadCount,
                    LastMessage = latestMessage != null
                        ? new LastMessageDto
                        {
                            Content = latestMessage.Content,
                            SenderId = latestMessage.SenderId,
                            CreatedAt = latestMessage.CreatedAt
                        }
                        : null
                };

            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancel);
        }

        private async Task EnsureDirectParticipantsAreFriendsAsync(string userLow, string userHigh, CancellationToken cancel)
        {
            var areFriends = await _dbcontext.Friendships
                .AsNoTracking()
                .AnyAsync(f =>
                    f.UserLowId == userLow &&
                    f.UserHighId == userHigh &&
                    f.Status == AcceptedFriendshipStatus,
                    cancel);

            if (!areFriends)
                throw new ForbiddenException(ErrorCodes.AUTH.FORBIDDEN, "Direct conversations are only available between friends.");
        }

        private async Task<Conversation?> TryGetExistingDirectConversationAsync(string userLow, string userHigh, CancellationToken cancel)
        {
            return await (
                from key in _dbcontext.ConversationKeys.AsNoTracking()
                join conversation in _dbcontext.Conversations.AsNoTracking() on key.ConversationId equals conversation.Id
                where key.UserLowId == userLow && key.UserHighId == userHigh
                select conversation)
                .FirstOrDefaultAsync(cancel);
        }

        private static ConversationResponse MapConversationResponse(Conversation conversation)
        {
            return new ConversationResponse
            {
                Id = conversation.Id,
                Name = conversation.Name,
                AvatarUrl = conversation.AvatarUrl,
                Type = conversation.Type,
                LastMessageAt = conversation.LastMessageAt
            };
        }

        private static bool IsDirectConversation(string? type)
        {
            return string.Equals(type, "direct", StringComparison.OrdinalIgnoreCase);
        }
    }
}
