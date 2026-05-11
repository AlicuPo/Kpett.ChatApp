using System.ComponentModel;

namespace Kpett.ChatApp.Enums
{
    public enum NotificationType
    {
        [Description("FriendRequestReceived")]
        FriendRequestReceived = 0,

        [Description("FriendRequestAccepted")]
        FriendRequestAccepted = 1,

        [Description("CommentMention")]
        CommentMention = 2
    }
}
