namespace Kpett.ChatApp.DTOs.Request.Friend
{
    /// <summary>
    /// Request lấy danh sách bạn bè chưa tham gia vào một nhóm chat.
    /// Dùng cho chức năng thêm thành viên vào group.
    /// </summary>
    public class GetFriendsNotInGroupRequest
    {
        /// <summary>
        /// ID của cuộc hội thoại nhóm (sẽ được gán từ route parameter)
        /// </summary>
        public string ConversationId { get; set; } = null!;

        /// <summary>
        /// Từ khóa tìm kiếm theo Username hoặc DisplayName
        /// </summary>
        public string? Search { get; set; }

        /// <summary>
        /// Cursor phân trang (base64 encoded)
        /// </summary>
        public string? Cursor { get; set; }

        /// <summary>
        /// Số lượng item mỗi trang (mặc định 20, tối đa 50)
        /// </summary>
        public int Limit { get; set; } = 20;
    }
}
