using MediatR;

namespace Kpett.ChatApp.Events.Conversation;

public class MessageMentionedEvent : INotification
{
    public string ConversationId { get; set; } = null!;
    public string MessageId { get; set; } = null!;
    public string ActorId { get; set; } = null!;
    public List<string> MentionedUserIds { get; set; } = null!;
    public string TextSnippet { get; set; } = string.Empty;
}