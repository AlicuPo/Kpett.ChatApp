namespace Kpett.ChatApp.Helper
{
    public record UserClaims(
    string UserId,
    string Username,
    //string Email,
    string? DisplayName = null,
    string? AvatarUrl = null,
    DateTime? ExpiresAt = null,
    DateTime? IssuedAt = null,
    List<string>? Roles = null
)
    {
        public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
        public bool IsInRole(string role) => Roles?.Contains(role, StringComparer.OrdinalIgnoreCase) ?? false;
    }

}

