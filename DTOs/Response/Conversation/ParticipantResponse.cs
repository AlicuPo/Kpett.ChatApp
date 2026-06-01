using Kpett.ChatApp.DTOs.Response.User;

namespace Kpett.ChatApp.DTOs.Response.Conversation
{
    public class ParticipantResponse
    {
        public string Id { get; set; } = null!;
        public string? Username { get; set; } = null!;
        public string? DisplayName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public string Role { get; set; } = null!;
        public bool IsOnline { get; set; }
        public string? LastReadMessageId { get; set; }
        public bool IsFriend { get; set; }
    }
}
