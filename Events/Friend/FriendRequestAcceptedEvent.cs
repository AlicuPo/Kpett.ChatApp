using MediatR;

namespace Kpett.ChatApp.Events.Friend
{
    public class FriendRequestAcceptedEvent : INotification
    {
        public string RequestId { get; set; } = null!;
        public string AccepterId { get; set; } = null!;
        public string RequesterId { get; set; } = null!;
    }
}
