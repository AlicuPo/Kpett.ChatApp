using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.DTOs.Response.User
{
    public class UserMediaResponse
    {
        public string Id { get; set; } = null!;
        public string Url { get; set; } = null!;
        public string MediaType { get; set; } = null!;
        public string MimeType { get; set; } = null!;
        public bool IsPrimary { get; set; } = false;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
