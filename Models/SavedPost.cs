using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public class SavedPost
{
    [MaxLength(450)]
    public string Id { get; set; } = null!;

    [MaxLength(450)]
    public string UserId { get; set; } = null!;

    [MaxLength(450)]
    public string PostId { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
