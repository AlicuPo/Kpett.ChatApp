namespace Kpett.ChatApp.DTOs.Request.Conversation
{
    public class UpdateConversationSettingsRequest
    {
        public bool? IsMuted { get; set; }
        public bool? IsArchived { get; set; }
    }
}
