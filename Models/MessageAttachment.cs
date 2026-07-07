using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public class MessageAttachment
{
    [Key]
    [MaxLength(450)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(450)]
    public string MessageId { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = "image";

    [Required]
    public string Url { get; set; } = null!;

    [MaxLength(450)]
    public string? PublicId { get; set; }

    [MaxLength(255)]
    public string? Filename { get; set; }

    public long? FileSize { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
