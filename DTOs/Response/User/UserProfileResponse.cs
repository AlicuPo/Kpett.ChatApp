namespace Kpett.ChatApp.DTOs.Response.User
{
    public class UserProfileResponse: UserResponse
    {
        public string? Biography { get; set; }
        public string? Cocupation { get; set; }
        public string? Location { get; set; }
        public string? CoverUrl { get; set; }
        public UserStatsResponse? Stats { get; set; }
        public ProfileViewerContext? ViewerContext { get; set; }
        public bool IsOnline { get; set; }
    }
}
