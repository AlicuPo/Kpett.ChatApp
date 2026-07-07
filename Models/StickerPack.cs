using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public class StickerPack
{
    [Key]
    [MaxLength(450)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = null!;

    [MaxLength(500)]
    public string? Description { get; set; }

    public string? ThumbnailUrl { get; set; }

    [Required]
    [MaxLength(450)]
    public string OwnerId { get; set; } = null!;

    public bool IsPublic { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
