using Kpett.ChatApp.Enums;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.DTOs.Request.Group
{
    public class UpdateGroupRequest
    {

        public string? Name { get; set; }

        public string? Description { get; set; }

        public string? Privacy { get; set; }

        public string? AvatarUrl { get; set; }

        public string? CoverImageUrl { get; set; }

        public string? Language { get; set; }

        public List<string>? Rules { get; set; }
    }
}
