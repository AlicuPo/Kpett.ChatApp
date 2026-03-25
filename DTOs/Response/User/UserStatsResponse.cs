namespace Kpett.ChatApp.DTOs.Response.User
{
    public class UserStatsResponse: UserResponse
    {
        public int TotalPosts { get; set; }
        public int Followers { get; set; }
        public int Following { get; set; }
    }
}
