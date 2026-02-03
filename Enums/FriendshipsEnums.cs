using System.ComponentModel;

namespace Kpett.ChatApp.Enums
{
    public enum FriendshipsEnums
    {
        [Description("Pending")]
        Pending = 1,

        [Description("Accepted")]
        Accepted = 2,

        [Description("Rejected")]
        Rejected = 4,

        [Description("Cancelled")]
        Cancelled = 5
    }
}
