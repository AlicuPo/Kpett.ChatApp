using Kpett.ChatApp.DTOs;

namespace Kpett.ChatApp.Receive
{
    public interface INotificationService
    {
        Task CreateMessageNotificationsAsync(string conversationId, string senderId, MesSageDTO dto);
    }
}
