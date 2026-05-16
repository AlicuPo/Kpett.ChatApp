using Kpett.ChatApp.DTOs.Response.User;
using System.Text.Json.Serialization;

namespace Kpett.ChatApp.DTOs.Response.Conversation
{
    public class ConversationResponse
    {
        public string Id { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string? Name { get; set; }
        public string? AvatarUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastMessageAt { get; set; }
        public MessageSnippetResponse? LastMessage { get; set; }
        public List<ParticipantResponse> Participants { get; set; } = new();
        public bool IsActive { get; set; } = false;
        public bool HasUnread { get; set; }
        public ConversationViewerContextResponse? ViewerContext { get; set; }
    }
}
