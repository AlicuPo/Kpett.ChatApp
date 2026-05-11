using Kpett.ChatApp.Constants;
using System.Text.Json.Serialization;

namespace Kpett.ChatApp.DTOs.Response.Conversation.Metadata
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "actionType")]
    [JsonDerivedType(typeof(GroupCreatedMetadata), typeDiscriminator: MessageActionTypeConstants.GroupCreated)]
    [JsonDerivedType(typeof(MemberAddedMetadata), typeDiscriminator: MessageActionTypeConstants.MemberAdded)]
    [JsonDerivedType(typeof(MemberLeftMetadata), typeDiscriminator: MessageActionTypeConstants.MemberLeft)]
    [JsonDerivedType(typeof(MemberRemovedMetadata), typeDiscriminator: MessageActionTypeConstants.MemberRemoved)]
    public class SystemMessageMetadata
    {
        public SnapshotUser Actor { set; get; } = null!;
    }

    public record SnapshotUser
    {
        public required string Id { set; get; }

        public required string Name { set; get; }
    }
}

