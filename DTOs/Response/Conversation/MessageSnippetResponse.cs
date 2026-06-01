using Kpett.ChatApp.DTOs.Response.Conversation.Metadata;
using Kpett.ChatApp.Enums;
using System.Text.Json.Serialization;

namespace Kpett.ChatApp.DTOs.Response.Conversation
{
    public class MessageSnippetResponse
    {
        public string Id { get; set; } = null!;
        public string SenderId { get; set; } = null!;
        public string? SenderName { get; set; }
        public string Type { get; set; } = null!;
        public string? Content { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public SystemMessageMetadata? ActionMetadata { get; set; }
    }
}
