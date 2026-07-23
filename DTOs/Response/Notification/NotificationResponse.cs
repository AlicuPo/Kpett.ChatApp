namespace Kpett.ChatApp.DTOs.Response.Notification
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
        public NotificationSoundResponse Sound { get; set; } = NotificationSoundResponse.FromType(null);
    }

    public class ActorSnippetResponse
    {
        public string Id { get; set; } = null!;
        public string? DisplayName { get; set; } = null!;
        public string? Username { get; set; } = null!;
        public string? AvatarUrl { get; set; }
    }

    public class NotificationSoundResponse
    {
        private const string DefaultSoundKey = "notification_default";

        public bool Enabled { get; set; } = true;
        public string Key { get; set; } = DefaultSoundKey;
        public double Volume { get; set; } = 0.8;

        public static NotificationSoundResponse FromType(string? notificationType)
        {
            return new NotificationSoundResponse
            {
                Key = notificationType switch
                {
                    "FriendRequestReceived" => "friend_request",
                    "FriendRequestAccepted" => "friend_accept",
                    "CommentMention" => "comment_mention",
                    "GroupInvitationReceived" => "group_invitation",
                    _ => DefaultSoundKey
                }
            };
        }
    }
}
