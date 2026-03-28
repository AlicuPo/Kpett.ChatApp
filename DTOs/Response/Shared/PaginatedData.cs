namespace Kpett.ChatApp.DTOs.Response.Shared
{
    public class PaginatedData<T>
    {
        public List<T> Items { get; set; } = new List<T>();

        public CursorPaginationMeta Pagination { get; set; } = null!;
    }
}
