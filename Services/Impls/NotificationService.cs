using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Response.Message;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Receive;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
namespace Kpett.ChatApp.Services.Impls
{
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _dbContext;
        public NotificationService(AppDbContext dbContext)
        { 
            _dbContext = dbContext;
        }

        public async Task CreateMessageNotificationsAsync(string conversationId, string senderId, MessageDTO dto)
        {
            if (string.IsNullOrEmpty(conversationId))
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "conversationId not null or empty");
            }
            if (string.IsNullOrEmpty(senderId))
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "senderId not null or empty");
            }

            if (dto == null)
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "message dto not null");
            }

            var recipientIds = await _dbContext.ConversationParticipants
                .AsNoTracking()
                .Where(cp => cp.ConversationId == conversationId && cp.UserId != senderId)
                .Select(cp => cp.UserId)
                .ToListAsync();

            if (recipientIds.Count == 0)
            {
                return;
            }

            var payload = new Dictionary<string, object?>
            {
                ["conversationId"] = conversationId
            };

            if (dto.Id.HasValue)
            {
                payload["messageId"] = dto.Id.Value;
            }

            var now = DateTime.UtcNow;
            var data = JsonSerializer.Serialize(payload);
            var notifications = recipientIds.Select(recipientId => new Notification
            {
                Id = Guid.NewGuid().ToString(),
                UserId = recipientId,
                SenderId = senderId,
                Type = "MESSAGE",
                Content = dto.Content,
                Data = data,
                IsRead = false,
                CreatedAt = now
            }).ToList();

            await _dbContext.Notifications.AddRangeAsync(notifications);
            await _dbContext.SaveChangesAsync();
        }
    }
}
