using System.Text.Json.Serialization;

namespace Kpett.ChatApp.DTOs.Response.Shared
{
    public class CursorPaginationMeta
    {
        public string? NextCursor { get; set; }

        public bool HasMore => !string.IsNullOrEmpty(NextCursor);

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TotalCount { get; set; }

        public int Limit { get; set; }
    }
}
