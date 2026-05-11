using MediatR;

namespace Kpett.ChatApp.Events.Comment
{
    public class CommentMentionedEvent : INotification
    {
        public string PostId { get; set; } = null!;
        public string CommentId { get; set; } = null!;
        public string ActorId { get; set; } = null!;
        public List<string> MentionedUserIds { get; set; } = null!;
        public string CommentSnippet { get; set; } = string.Empty!;
    }
}
