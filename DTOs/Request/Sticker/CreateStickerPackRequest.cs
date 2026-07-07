namespace Kpett.ChatApp.DTOs.Request.Sticker
{
    public class CreateStickerPackRequest
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsPublic { get; set; } = false;
    }
}
