namespace Kpett.ChatApp.DTOs.Request.User
{
    public class AccountSetupRequest
    {
        public string Username { get; set; } = null!;
        public string DisplayName { get; set; } = null!;
        public string? Biography { get; set; }
        public List<string> Interests { get; set; } = new List<string>();
    }
}
