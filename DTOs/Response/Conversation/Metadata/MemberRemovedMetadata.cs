namespace Kpett.ChatApp.DTOs.Response.Conversation.Metadata
{
    public class MemberRemovedMetadata : SystemMessageMetadata
    {
        public List<SnapshotUser> Targets { get; init; } = new();
    }
}
