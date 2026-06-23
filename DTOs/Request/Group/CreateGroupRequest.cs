using Kpett.ChatApp.Enums;
using System.ComponentModel.DataAnnotations;
using Kpett.ChatApp.Helper;

namespace Kpett.ChatApp.DTOs.Request.Group
{
    public class CreateGroupRequest
    {

        public string? Name { get; set; }

        public string? Description { get; set; }

        public string? type { get; set; }

        public string? AvatarUrl { get; set; }

        public string? CoverImageUrl { get; set; }

        public string? Language { get; set; } = "vi";

        public List<string> Rules { get; set; } = new();
    }
}
