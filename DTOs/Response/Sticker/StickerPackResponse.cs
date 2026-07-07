namespace Kpett.ChatApp.DTOs.Response.Sticker
{
    public class StickerPackResponse
    {
        public string Id { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string OwnerId { get; set; } = null!;
        public bool IsPublic { get; set; }
        public int StickerCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<StickerResponse>? Stickers { get; set; }
    }

    public class StickerResponse
    {
        public string Id { get; set; } = null!;
        public string StickerPackId { get; set; } = null!;
        public string MediaUrl { get; set; } = null!;
        public string? PublicId { get; set; }
        public string? Emoji { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public long? FileSize { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
