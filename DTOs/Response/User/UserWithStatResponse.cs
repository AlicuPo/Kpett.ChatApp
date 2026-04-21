namespace Kpett.ChatApp.DTOs.Response.User
{
    public class UserWithStatResponse : UserResponse
    {
        public UserStatsResponse? Stats { get; set; }
    }
}
