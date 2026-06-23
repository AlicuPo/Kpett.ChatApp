using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.DTOs.Response.Group
{
    public class CreateGroupResponse
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Slug { get; set; }
        public DateTime CreatedAt { get; set; }

    }
}
