namespace Kpett.ChatApp.DTOs.Response.Conversation.Metadata
{
    public class MemberAddedMetadata : SystemMessageMetadata
    {
        public List<SnapshotUser> Targets { get; init; } = new();
    }
}
