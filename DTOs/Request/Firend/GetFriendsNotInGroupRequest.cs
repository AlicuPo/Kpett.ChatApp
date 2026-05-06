using System.Text.Json.Serialization;

namespace Kpett.ChatApp.DTOs.Request.Firend
{
    public class GetFriendsNotInGroupRequest
    {
        [JsonIgnore]
        public string ConversationId { get; set; } = null!;
        public string? Search { get; set; }
        public int Limit { get; set; } = 12;
        public string? Cursor { get; set; }
    }
}
