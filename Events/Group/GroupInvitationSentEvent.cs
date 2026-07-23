using MediatR;

namespace Kpett.ChatApp.Events.Group
{
    public class GroupInvitationSentEvent : INotification
    {
        public string InvitationId { get; set; } = null!;
        public string GroupId { get; set; } = null!;
        public string GroupName { get; set; } = null!;
        public string InviterId { get; set; } = null!;
        public string InviteeId { get; set; } = null!;
    }
}
