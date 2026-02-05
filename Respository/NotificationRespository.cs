using Kpett.ChatApp.DTOs;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Receive;
using Kpett.ChatApp.Services;
using Microsoft.EntityFrameworkCore;
namespace Kpett.ChatApp.Respository
{
    public class NotificationRespository : INotificationService
    {
        private readonly AppDbContext _dbContext;
        public NotificationRespository(AppDbContext dbContext)
        { 
            _dbContext = dbContext;
        }

        public Task CreateMessageNotificationsAsync(string conversationId, string senderId, MessageDTO dto)
        {
            if (string.IsNullOrEmpty(conversationId))
            {
                throw new AppException(StatusCodes.Status400BadRequest,"conversationId not null or empty");
            }
            if (string.IsNullOrEmpty(senderId))
            {
                throw new AppException(StatusCodes.Status400BadRequest,"senderId not null or empty");
            }
            return Task.Run(async () =>
            {
                var participants = await _dbContext.ConversationParticipants
                    .Where(cp => cp.ConversationId == conversationId && cp.UserId != senderId)
                    .ToListAsync();
                var notifications = participants.Select(participant => new Notification
                {
                    UserId = participant.UserId,
                    //MessageId = dto.Id!.Value,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                }).ToList();
                await _dbContext.Notifications.AddRangeAsync(notifications);
                await _dbContext.SaveChangesAsync();
            });
        }
    }
}
