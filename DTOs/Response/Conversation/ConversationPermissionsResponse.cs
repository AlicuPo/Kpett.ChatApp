using System.Text.Json.Serialization;

namespace Kpett.ChatApp.DTOs.Response.Conversation
{
    public class ConversationPermissionsResponse
    {
        public bool CanSendMessage { get; init; }

        public bool CanAddParticipants { get; init; }

        public bool CanRemoveParticipants { get; init; }

        public bool CanChangeName { get; init; }

        public bool CanModerateMessages { get; init; }
    }
}
