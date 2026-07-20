namespace Kpett.ChatApp.DTOs.Response.Group
{
    /// <summary>
    /// Thông tin người dùng bị bỏ qua khi mời vào nhóm kèm lý do.
    /// </summary>
    public class GroupInviteSkippedResponse
    {
        /// <summary>ID của người dùng bị bỏ qua.</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Lý do bỏ qua. Giá trị: "self", "user_not_found", "not_friend", "already_member", "blocked", "join_request_pending", "invitation_pending".
        /// </summary>
        public string Reason { get; set; } = string.Empty;
    }
}
