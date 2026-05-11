using Kpett.ChatApp.Models;
using MediatR;

namespace Kpett.ChatApp.Events.Friend
{
    public class FriendRequestSentEvent : INotification
    {
        public string RequestId { get; set; } = null!;
        public string SenderId { get; set; } = null!;
        public string ReceiverId { get; set; } = null!;
    }
}
