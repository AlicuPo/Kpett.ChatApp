using System.Text.Json.Serialization;

namespace Kpett.ChatApp.DTOs.Request.Conversation
{
    public class AddMembersRequest
    {
        [JsonIgnore]
        public string ConversationId { get; set; } = null!;

        public List<string> UserIdsToAdd { get; set; } = new List<string>();
    }
}
