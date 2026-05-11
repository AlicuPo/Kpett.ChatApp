namespace Kpett.ChatApp.DTOs.Request.User
{
    public class UpdateGeneralInfoUserRequest
    {
        public string Username { get; set; } = null!;
        public string DisplayName { get; set; } = null!;
        public string? Occupation { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Biography { get; set; }
        public string? Location { get; set; }
    }
}
