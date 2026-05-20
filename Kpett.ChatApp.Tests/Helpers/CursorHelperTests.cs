using Kpett.ChatApp.Helper;

namespace Kpett.ChatApp.Tests.Helpers;

public class CursorHelperTests
{
    [Fact]
    public void EncodeDecode_RoundTripsPayload()
    {
        var payload = new TestCursorPayload("post-123", new DateTime(2026, 5, 16, 3, 4, 5, DateTimeKind.Utc));

        var cursor = CursorHelper.Encode(payload);
        var decoded = CursorHelper.Decode<TestCursorPayload>(cursor);

        Assert.NotNull(decoded);
        Assert.Equal(payload.Id, decoded.Id);
        Assert.Equal(payload.CreatedAt, decoded.CreatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-base64")]
    public void Decode_ReturnsDefault_WhenCursorIsInvalid(string cursor)
    {
        var decoded = CursorHelper.Decode<TestCursorPayload>(cursor);

        Assert.Null(decoded);
    }

    private sealed record TestCursorPayload(string Id, DateTime CreatedAt);
}
