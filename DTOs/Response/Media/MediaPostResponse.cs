namespace Kpett.ChatApp.DTOs.Response.Media
{
    public class MediaPostResponse
    {
        public string PublicId { get; set; } = null!;
        public string? Url { get; set; }
        public string? Type { get; set; }
    }
}
