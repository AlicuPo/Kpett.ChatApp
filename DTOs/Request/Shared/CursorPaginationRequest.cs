namespace Kpett.ChatApp.DTOs.Request.Shared
{
    public class CursorPaginationRequest
    {
        public string? Cursor { get; set; }
        public int Limit { get; set; } = 10;
    }
}
