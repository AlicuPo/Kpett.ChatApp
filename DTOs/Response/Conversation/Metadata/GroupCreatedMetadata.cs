namespace Kpett.ChatApp.DTOs.Response.Conversation.Metadata
{
    public class GroupCreatedMetadata : SystemMessageMetadata
    {
        public List<SnapshotUser> Targets { get; set; } = new();
    }
}
