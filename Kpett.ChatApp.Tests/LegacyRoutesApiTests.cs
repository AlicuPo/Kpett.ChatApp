using System.Net;
using System.Net.Http.Json;
using Kpett.ChatApp.Tests.Infrastructure;

namespace Kpett.ChatApp.Tests;

public class LegacyRoutesApiTests
{
    [Theory]
    [InlineData("POST", "/api/posts/legacy/post-feed")]
    [InlineData("POST", "/api/conversations/CreateConversations")]
    [InlineData("GET", "/api/messages/GetMessagesAsync")]
    [InlineData("POST", "/api/notifications/Notification")]
    public async Task LegacyRoutes_ReturnNotFound(string method, string path)
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateApiClient();

        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (!HttpMethod.Get.Method.Equals(method, StringComparison.OrdinalIgnoreCase)
            && !HttpMethod.Delete.Method.Equals(method, StringComparison.OrdinalIgnoreCase))
        {
            request.Content = JsonContent.Create(new { });
        }

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
