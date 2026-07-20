using Kpett.ChatApp.Enums;
using System.ComponentModel.DataAnnotations;
using Kpett.ChatApp.Helpers;

namespace Kpett.ChatApp.DTOs.Request.Group
{
    /// <summary>
    /// Yêu c?u t?o nhóm m?i.
    /// </summary>
    public class CreateGroupRequest
    {
        /// <summary>Tên nhóm (b?t bu?c).</summary>
        public string? Name { get; set; }

        /// <summary>Mô t? nhóm (tùy ch?n).</summary>
        public string? Description { get; set; }

        /// <summary>Lo?i quy?n riêng tý: "public" | "private" | "hidden" (m?c ð?nh: "public").</summary>
        public string? Type { get; set; }

        /// <summary>URL ?nh ð?i di?n nhóm (tùy ch?n).</summary>
        public string? AvatarUrl { get; set; }

        /// <summary>URL ?nh b?a nhóm (tùy ch?n).</summary>
        public string? CoverImageUrl { get; set; }

        /// <summary>Ngôn ng? nhóm (m?c ð?nh: "vi").</summary>
        public string? Language { get; set; } = "vi";

        /// <summary>Danh sách n?i quy nhóm (m?ng chu?i, tùy ch?n).</summary>
        public List<string> Rules { get; set; } = new();

        /// <summary>
        /// Danh sách ID ngý?i dùng ðý?c m?i ngay khi t?o nhóm (tùy ch?n, t?i ða 100).
        /// Ngý?i dùng ph?i là b?n bè ðang active và chýa là thành viên nhóm.
        /// </summary>
        public List<string> InviteeIds { get; set; } = new();
    }
}
