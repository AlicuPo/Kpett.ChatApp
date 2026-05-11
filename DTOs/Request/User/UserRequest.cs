using Kpett.ChatApp.DTOs.Request.Shared;

namespace Kpett.ChatApp.DTOs.Request.User
{
    public class UserRequest : SearchRequest
    {
        public string? Id { get; set; }
    }
}
