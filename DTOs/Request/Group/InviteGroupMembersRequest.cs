namespace Kpett.ChatApp.DTOs.Request.Group
{
    /// <summary>
    /// Yêu cầu mời danh sách người dùng vào nhóm.
    /// </summary>
    public class InviteGroupMembersRequest
    {
        /// <summary>
        /// Danh sách ID người dùng cần mời. Tối đa 100 người một lần.
        /// Chỉ mời được bạn bè đang active; các trường hợp không hợp lệ sẽ được liệt kê trong <see cref="GroupInviteMembersResponse.Skipped"/>.
        /// </summary>
        public List<string> UserIds { get; set; } = new();
    }
}
