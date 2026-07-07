using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public class Sticker
{
    [Key]
    [MaxLength(450)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(450)]
    public string StickerPackId { get; set; } = null!;

    [Required]
    public string MediaUrl { get; set; } = null!;

    [MaxLength(450)]
    public string? PublicId { get; set; }

    [MaxLength(50)]
    public string? Emoji { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public long? FileSize { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
