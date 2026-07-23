using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Helpers;

namespace Kpett.ChatApp.DTOs.Request.Shared
{
    public class SearchRequest
    {
        public string? Search { get; set; }    
        public int? Status { get; set; }
        public string Type { get; set; } = PostType.Post.GetDescription();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 40;
        public string? SortBy { get; set; } = "CreateAt";
        public string? SortOrder { get; set; } = "desc";
    }
}
