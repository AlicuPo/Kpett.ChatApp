namespace Kpett.ChatApp.DTOs.Response.User
{
    public class UserProfileResponse : UserResponse
    {
        public DateTime? DateOfBirth { get; set; }
        public string? Biography { get; set; }
        public string? Occupation { get; set; }
        public string? Location { get; set; }
        public string? CoverUrl { get; set; }
        public UserStatsResponse? Stats { get; set; }
        public UserProfileViewerContextResponse? ViewerContext { get; set; }
        public bool IsOnline { get; set; }
    }
}
