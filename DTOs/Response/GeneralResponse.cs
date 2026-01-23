using System.Text.Json.Serialization;

namespace Kpett.ChatApp.DTOs.Response
{
   
    public record GeneralResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Return { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Message { get; init; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? StatusCode { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? ErorrCode { get; set; }
    }

    public record GeneralResponse<T> : GeneralResponse
    {
        public T Data { get; init; } = default!;

    }

    public record DataListResponse<T> : GeneralResponse
    {
        public List<T> Data { get; init; } = default!;
        public int TotalCount { get; init; }
    }

}
