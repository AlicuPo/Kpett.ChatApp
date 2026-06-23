using Kpett.ChatApp.Enums;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.DTOs.Request.Group
{
    public class SearchGroupRequest
    {
        public string? Keyword { get; set; }
        public string? Type { get; set; } // lọc theo loại nhóm
        public string? Language { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        public GroupSortBy SortBy { get; set; } = GroupSortBy.Relevance;
    }
}
