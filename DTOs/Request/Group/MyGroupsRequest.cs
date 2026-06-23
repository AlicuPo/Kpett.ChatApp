using Kpett.ChatApp.Enums;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.DTOs.Request.Group
{
    public class MyGroupsRequest
    {
        public string? FilterByRole { get; set; } // "admin" | "moderator" | "member" | null
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
