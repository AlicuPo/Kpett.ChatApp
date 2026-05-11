using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Kpett.ChatApp.DTOs.Response.Conversation
{
    public class ConversationViewerContextResponse
    {
        public int UnreadCount { get; init; }

        public bool IsMuted { get; init; }

        public bool IsPinned { get; init; }

        public bool IsArchived { get; init; }

        public string? LastReadMessageId { get; init; }

        public ConversationPermissionsResponse Permissions { get; init; } = new();
    }
}
