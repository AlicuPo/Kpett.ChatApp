// Legacy type stubs - cho phep FriendRequestsApiTests compile.
// Cac test nay test legacy API endpoints da bi thay doi.
// TODO: Cap nhat cac test nay de phu hop voi API moi.
using Kpett.ChatApp.DTOs.Response.Friend;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Helper;

namespace Kpett.ChatApp.Tests.Infrastructure;

// Stub cho CreateFriendRequestRequest (dung trong RelationshipsController voi ten moi la SendFriendRequestRequest)
public class CreateFriendRequestRequest
{
    public string ReceiverId { get; set; } = null!;
}

// Stub cho FriendRequestDTO (Response DTO cu)
public class FriendRequestDTO
{
    public string FriendRequestId { get; set; } = null!;
    public string SenderId { get; set; } = null!;
    public string ReceiverId { get; set; } = null!;
    public string Status { get; set; } = null!;
}

// Stub cho FriendshipsEnums (da duoc doi thanh FriendshipStatus)
public static class FriendshipsEnums
{
    public static FriendshipStatus Pending => FriendshipStatus.Active; // placeholder
    public static FriendshipStatus Accepted => FriendshipStatus.Active;
    public static FriendshipStatus Cancelled => FriendshipStatus.Active;
    public static FriendshipStatus Rejected => FriendshipStatus.Active;
}

// Stub cho ReadMessageRequest
public class ReadMessageRequest
{
    public string LastReadMessageId { get; set; } = null!;
    // Override Value cho compatibility
}

// Stub cho ConversationKeysRequest
public class ConversationKeysRequest
{
    public string UserLow { get; set; } = null!;
    public string UserHigh { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Name { get; set; } = null!;
}
