namespace Kpett.ChatApp.DTOs.Response.Group
{
    /// <summary>
    /// Thông tin một lời mời tham gia nhóm.
    /// </summary>
    public class GroupInvitationResponse
    {
        /// <summary>ID của lời mời.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>ID của nhóm được mời.</summary>
        public string GroupId { get; set; } = string.Empty;

        /// <summary>Tên nhóm.</summary>
        public string? GroupName { get; set; }

        /// <summary>ID của người gửi lời mời.</summary>
        public string InvitedByUserId { get; set; } = string.Empty;

        /// <summary>Tên hiển thị của người gửi lời mời.</summary>
        public string? InviterName { get; set; }

        /// <summary>ID của người được mời.</summary>
        public string InviteeUserId { get; set; } = string.Empty;

        /// <summary>Trạng thái lời mời: "pending" | "accepted" | "declined".</summary>
        public string Status { get; set; } = "pending";

        /// <summary>Thời điểm tạo lời mời (UTC).</summary>
        public DateTime CreatedAt { get; set; }
    }
}
