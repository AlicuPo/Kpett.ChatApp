using Kpett.ChatApp.Contants;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Kpett.ChatApp.Services.Impls
{
    public class ConversationAccessService : IConversationAccessService
    {
        private readonly AppDbContext _dbContext;

        public ConversationAccessService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task EnsureCanAccessConversationAsync(string conversationId, string userId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Conversation ID cannot be null or empty.");

            if (string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");

            var conversationExists = await _dbContext.Conversations
                .AsNoTracking()
                .AnyAsync(c => c.Id == conversationId, cancellationToken);

            if (!conversationExists)
                throw new NotFoundException(ErrorCodes.CONVERSATION.NOT_FOUND, "Conversation not found.");

            var isParticipant = await _dbContext.ConversationParticipants
                .AsNoTracking()
                .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId, cancellationToken);

            if (!isParticipant)
                throw new ForbiddenException(
                    ErrorCodes.CONVERSATION.USER_NOT_IN_CONVERSATION,
                    "User is not a participant of this conversation.");
        }
    }
}
