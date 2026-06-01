namespace Kpett.ChatApp.Enums
{
    public enum MessageActionType
    {
        None = 0,

        // Các sự kiện liên quan đến cấu hình nhóm
        GroupCreated = 1,
        GroupNameChanged = 2,
        GroupAvatarChanged = 3,
        GroupThemeChanged = 4,

        // Các sự kiện liên quan đến thành viên
        MemberAdded = 10,
        MemberRemoved = 11,
        MemberLeft = 12,
        MemberJoinedViaLink = 13,
        AdminPromoted = 14,
        AdminDemoted = 15,

        // Các sự kiện tương tác trong khung chat
        MessagePinned = 20,
        MessageUnpinned = 21,

        // Các sự kiện liên quan đến cuộc gọi (nếu có WebRTC/SignalR)
        CallStarted = 30,
        CallEnded = 31,
        CallMissed = 32
    }
}
