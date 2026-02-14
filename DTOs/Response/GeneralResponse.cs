using System.Text.Json.Serialization;

namespace Kpett.ChatApp.DTOs.Response
{
   
    public record GeneralResponse
    {
        public bool IsSuccess { get; init; }

        public string Message { get; init; } = string.Empty;

        public int StatusCode { get; set; }
    }

    public record ErrorResponse : GeneralResponse
    {
        public string ErrorCode { get; init; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? StackTrace { get; init; }
    }

    public record GeneralResponse<T> : GeneralResponse
    {
        public T Data { get; init; } = default!;
    }

    public record DataListResponse<T> : GeneralResponse
    {
        public List<T> Data { get; init; } = default!;
        public int PageNo { get; init; }
        public int PageSize { get; init; }

        public int TotalCount { get; init; }
        public int TotalPages { get; init; }
    }

}
