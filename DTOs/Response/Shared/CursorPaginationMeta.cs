namespace Kpett.ChatApp.DTOs.Response.Shared
{
    public class CursorPaginationMeta
    {
        public string? NextCursor { get; set; }

        public bool HasMore => !string.IsNullOrEmpty(NextCursor);

        public int? TotalCount { get; set; }

        public int Limit { get; set; }
    }
}
