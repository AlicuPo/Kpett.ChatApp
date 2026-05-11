using Kpett.ChatApp.DTOs.Response;

namespace Kpett.ChatApp.DTOs.Request.Conversation
{
    public class CreateConversationRequest
    {
        public string Type { get; set; } = null!;
        public List<string> ParticipantIds { get; set; } = new();
        public string? InitialMessage { get; set; }

        public string? Name { get; set; }
    }
}



