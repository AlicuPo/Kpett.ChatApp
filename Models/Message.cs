using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class Message
{
    [Key]
    [MaxLength(450)]
    public string Id { get; set; } = null!;

    [Required]
    [MaxLength(450)]
    public string ConversationId { get; set; } = null!;

    [Required]
    [MaxLength(450)]
    public string SenderId { get; set; } = null!;

    [MaxLength(450)]
    public string? ReplyToMessageId { get; set; }

    public string? Content { get; set; }

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = "Text";

    [MaxLength(450)]
    public string? ClientMessageId { get; set; }

    public string? Metadata { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; } = false;
}