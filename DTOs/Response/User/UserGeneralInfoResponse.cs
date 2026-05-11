namespace Kpett.ChatApp.DTOs.Response.User
{
    public class UserGeneralInfoResponse : UserResponse
    {
        public string? CoverUrl { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Biography { get; set; }
        public string? Occupation { get; set; }
        public string? Location { get; set; }
    }
}
