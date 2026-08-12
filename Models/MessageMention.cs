using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class MessageMention
{
    [Key]
    [MaxLength(450)]
    public string Id { get; set; } = null!;

    [Required]
    [MaxLength(450)]
    public string MessageId { get; set; } = null!;

    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = null!;

    public string Username { get; set; } = null!;

    public string? DisplayName { get; set; }

    public bool IsNotified { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}