using Kpett.ChatApp.Enums;
using System.ComponentModel.DataAnnotations;
using Kpett.ChatApp.Helper;

namespace Kpett.ChatApp.DTOs.Request.Group
{
    /// <summary>
    /// Yêu cầu tạo nhóm mới.
    /// </summary>
    public class CreateGroupRequest
    {
        /// <summary>Tên nhóm (bắt buộc).</summary>
        public string? Name { get; set; }

        /// <summary>Mô tả nhóm (tùy chọn).</summary>
        public string? Description { get; set; }

        /// <summary>Loại quyền riêng tư: "public" | "private" | "hidden" (mặc định: "public").</summary>
        public string? Type { get; set; }

        /// <summary>URL ảnh đại diện nhóm (tùy chọn).</summary>
        public string? AvatarUrl { get; set; }

        /// <summary>URL ảnh bìa nhóm (tùy chọn).</summary>
        public string? CoverImageUrl { get; set; }

        /// <summary>Ngôn ngữ nhóm (mặc định: "vi").</summary>
        public string? Language { get; set; } = "vi";

        /// <summary>Danh sách nội quy nhóm (mảng chuỗi, tùy chọn).</summary>
        public List<string> Rules { get; set; } = new();

        /// <summary>
        /// Danh sách ID người dùng được mời ngay khi tạo nhóm (tùy chọn, tối đa 100).
        /// Người dùng phải là bạn bè đang active và chưa là thành viên nhóm.
        /// </summary>
        public List<string> InviteeIds { get; set; } = new();
    }
}
