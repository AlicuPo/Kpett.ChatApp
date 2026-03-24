using System.Text.Json;
using Kpett.ChatApp.DTOs.Response.Shared;

namespace Kpett.ChatApp.Tests.Infrastructure;

internal static class HttpTestHelpers
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<(string Raw, T Value)> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(raw));

        var value = JsonSerializer.Deserialize<T>(raw, JsonOptions);
        Assert.NotNull(value);

        return (raw, value!);
    }

    public static async Task<ErrorResponse> ReadErrorAsync(HttpResponseMessage response)
    {
        var (_, value) = await ReadJsonAsync<ErrorResponse>(response);
        return value;
    }

    public static async Task AssertNoContentAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(string.IsNullOrEmpty(raw), $"Expected empty response body but got: {raw}");
    }

    public static void AssertRawSuccessPayload(string raw)
    {
        using var json = JsonDocument.Parse(raw);
        var root = json.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        Assert.False(root.TryGetProperty("data", out _));
        Assert.False(root.TryGetProperty("isSuccess", out _));
    }
}
