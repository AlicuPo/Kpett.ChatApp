using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Helper;

namespace Kpett.ChatApp.Constants
{
    public class MessageActionTypeConstants
    {
        public const string GroupCreated = nameof(GroupCreated);
        public const string GroupNameChanged = nameof(GroupNameChanged);
        public const string GroupAvatarChanged = nameof(GroupAvatarChanged);
        public const string GroupThemeChanged = nameof(GroupThemeChanged);

        public const string MemberAdded = nameof(MemberAdded);
        public const string MemberRemoved = nameof(MemberRemoved);
        public const string MemberLeft = nameof(MemberLeft);
        public const string MemberJoinedViaLink = nameof(MemberJoinedViaLink);
        public const string AdminPromoted = nameof(AdminPromoted);
        public const string AdminDemoted = nameof(AdminDemoted);

        public const string MessagePinned = nameof(MessagePinned);
        public const string MessageUnpinned = nameof(MessageUnpinned);

        public const string CallStarted = nameof(CallStarted);
        public const string CallEnded = nameof(CallEnded);
        public const string CallMissed = nameof(CallMissed);
    }
}

