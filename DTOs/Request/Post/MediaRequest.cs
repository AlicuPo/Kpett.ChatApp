namespace Kpett.ChatApp.DTOs.Request.Post
{
    public class MediaRequest
    {
        public string PublicId { get; set; } = null!;
        public string SecureUrl { get; set; } = null!;
        public string? Format { get; set; }
        public string Type { get; set; } = null!;
    }
}
