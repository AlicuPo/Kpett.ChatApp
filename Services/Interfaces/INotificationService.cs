using Kpett.ChatApp.DTOs.Response.Message;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface INotificationService
    {
        Task CreateMessageNotificationsAsync(string conversationId, string senderId, MessageDTO dto);
    }
}

