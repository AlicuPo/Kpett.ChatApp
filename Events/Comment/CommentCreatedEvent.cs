using MediatR;

namespace Kpett.ChatApp.Events.Comment
{
    public class CommentCreatedEvent : INotification
    {
        public string PostId { get; set; } = null!;
        public string CommentId { get; set; } = null!;
        public string PostOwnerId { get; set; } = null!;
        public string ActorId { get; set; } = null!;
        public string CommentSnippet { get; set; } = string.Empty;
    }
}
