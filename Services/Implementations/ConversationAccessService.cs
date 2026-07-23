using Kpett.ChatApp.Data;
using Kpett.ChatApp.Constants;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Kpett.ChatApp.Services.Implementations
{
    /// <summary>Service ki?m tra quy?n truy c?p h?i tho?i.</summary>
    public class ConversationAccessService : IConversationAccessService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<ConversationAccessService> _logger;

        /// <summary>Kh?i t?o service v?i các dependencies.</summary>
        public ConversationAccessService(AppDbContext dbContext, ILogger<ConversationAccessService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task EnsureCanAccessConversationAsync(string conversationId, string userId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
            {
                _logger.LogWarning("Conversation access check rejected because conversation ID is empty");
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Conversation ID cannot be null or empty.");
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("Conversation access check rejected because user ID is empty");
                throw new UnauthorizedException(ErrorCodes.AUTH.UNAUTHORIZED, "User is not authenticated.");
            }

            _logger.LogDebug("Checking conversation access for user {UserId} and conversation {ConversationId}", userId, conversationId);

            var conversationExists = await _dbContext.Conversations
                .AsNoTracking()
                .AnyAsync(c => c.Id == conversationId, cancellationToken);

            if (!conversationExists)
            {
                _logger.LogWarning("Conversation access check failed because conversation {ConversationId} was not found", conversationId);
                throw new NotFoundException(ErrorCodes.CONVERSATION.NOT_FOUND, "Conversation not found.");
            }

            var isParticipant = await _dbContext.ConversationParticipants
                .AsNoTracking()
                .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId, cancellationToken);

            if (!isParticipant)
            {
                _logger.LogWarning("Conversation access denied for user {UserId} and conversation {ConversationId}", userId, conversationId);
                throw new ForbiddenException(ErrorCodes.CONVERSATION.USER_NOT_IN_CONVERSATION, "User is not a participant of this conversation.");
            }

            _logger.LogDebug("Conversation access granted for user {UserId} and conversation {ConversationId}", userId, conversationId);
        }
    }
}
