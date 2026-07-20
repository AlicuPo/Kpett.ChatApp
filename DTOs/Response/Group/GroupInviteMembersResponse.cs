namespace Kpett.ChatApp.DTOs.Response.Group
{
    /// <summary>
    /// Kết quả xử lý lời mời vào nhóm.
    /// </summary>
    public class GroupInviteMembersResponse
    {
        /// <summary>
        /// Danh sách lời mời đã được tạo thành công.
        /// </summary>
        public List<GroupInvitationResponse> Invitations { get; set; } = new();

        /// <summary>
        /// Danh sách người dùng bị bỏ qua kèm lý do (self, user_not_found, not_friend, already_member, blocked, join_request_pending, invitation_pending).
        /// </summary>
        public List<GroupInviteSkippedResponse> Skipped { get; set; } = new();

        /// <summary>Số lượng lời mời đã gửi thành công.</summary>
        public int InvitedCount => Invitations.Count;

        /// <summary>Số lượng người dùng bị bỏ qua.</summary>
        public int SkippedCount => Skipped.Count;
    }
}
