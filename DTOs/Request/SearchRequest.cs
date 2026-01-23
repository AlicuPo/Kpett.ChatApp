namespace Kpett.ChatApp.DTOs.Request
{
    public class SearchRequest
    {
        public string? Search { get; set; }    
        public int? Status { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 40;
        public string? SortBy { get; set; } = "CreateAt";
        public string? SortOrder { get; set; } = "desc";
    }
}
