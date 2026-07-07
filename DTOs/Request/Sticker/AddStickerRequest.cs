namespace Kpett.ChatApp.DTOs.Request.Sticker
{
    public class AddStickerRequest
    {
        public string MediaUrl { get; set; } = null!;
        public string? PublicId { get; set; }
        public string? Emoji { get; set; }
    }
}
