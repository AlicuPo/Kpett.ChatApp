using System.Text.Json.Serialization;

namespace Kpett.ChatApp.DTOs.Response.User
{
    public class UserResponse
    {
        public string Id { get; set; } = null!;
        public string? Username { get; set; }
        public string Email { get; set; } = null!;
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
        public bool IsProfileCompleted { get; set; }
        public bool IsVerified { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? LastActiveAt { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? CreatedAt { get; set; }
    }

}
