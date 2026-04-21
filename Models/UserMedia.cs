using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models
{
    public class UserMedia
    {
        [Key]
        public string Id { get; set; } = null!;

        [MaxLength(450)]
        public string UserId { get; set; } = null!;
        public string MediaUrl { get; set; } = null!;
        public string MediaType { get; set; } = null!;
        public string MimeType { get; set; } = null!;
        public bool IsPrimary { get; set; } = false;
        public bool IsTemporary { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
