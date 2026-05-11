namespace Kpett.ChatApp.DTOs.Response.Notidication
{
    public class NotificationResponse
    {
        public string Id { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string? ReferenceId { get; set; }
        public object? Metadata { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public ActorSnippetResponse? Actor { get; set; }
    }

    public class ActorSnippetResponse
    {
        public string Id { get; set; } = null!;
        public string? DisplayName { get; set; } = null!;
        public string? Username { get; set; } = null!;
        public string? AvatarUrl { get; set; }
    }
}
