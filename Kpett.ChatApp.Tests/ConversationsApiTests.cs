using System.Net;
using System.Net.Http.Json;
using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Request.Conversation;
using Kpett.ChatApp.DTOs.Request.Message;
using Kpett.ChatApp.DTOs.Response.Conversation;
using Kpett.ChatApp.DTOs.Response.Message;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Tests.Infrastructure;

namespace Kpett.ChatApp.Tests;

public class ConversationsApiTests
{
    [Fact]
    public async Task CreateConversation_ReturnsCreated_WithLocation()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("user-1", "user-1@example.com");

        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                TestData.CreateUser("user-1", "user-1@example.com"),
                TestData.CreateUser("user-2", "user-2@example.com"));
        });

        var response = await client.PostAsJsonAsync("/api/conversations", new ConversationKeysRequest
        {
            UserLow = "user-1",
            UserHigh = "user-2",
            Type = "direct",
            Name = "Chat"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var (raw, body) = await HttpTestHelpers.ReadJsonAsync<ConversationResponse>(response);
        HttpTestHelpers.AssertRawSuccessPayload(raw);
        Assert.Equal("Chat", body.Name);
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"/api/conversations/{body.Id}", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetConversations_ReturnsOk()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("user-1", "user-1@example.com");

        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                TestData.CreateUser("user-1", "user-1@example.com"),
                TestData.CreateUser("user-2", "user-2@example.com"));
            db.Conversations.Add(TestData.CreateConversation("conversation-1"));
            db.ConversationParticipants.AddRange(
                TestData.CreateConversationParticipant("participant-1", "conversation-1", "user-1"),
                TestData.CreateConversationParticipant("participant-2", "conversation-1", "user-2"));
        });

        var response = await client.GetAsync("/api/conversations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (raw, body) = await HttpTestHelpers.ReadJsonAsync<List<ConversationResponse>>(response);
        HttpTestHelpers.AssertRawSuccessPayload(raw);
        Assert.Single(body);
        Assert.Equal("conversation-1", body[0].Id);
    }

    [Fact]
    public async Task SendMessage_GetMessages_AndMarkRead_UseExpectedStatusCodes()
    {
        using var factory = new TestWebApplicationFactory();
        using var senderClient = factory.CreateAuthenticatedClient("sender-1", "sender@example.com");
        using var receiverClient = factory.CreateAuthenticatedClient("receiver-1", "receiver@example.com");

        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                TestData.CreateUser("sender-1", "sender@example.com"),
                TestData.CreateUser("receiver-1", "receiver@example.com"));
            db.Conversations.Add(TestData.CreateConversation("conversation-1"));
            db.ConversationParticipants.AddRange(
                TestData.CreateConversationParticipant("participant-1", "conversation-1", "sender-1"),
                TestData.CreateConversationParticipant("participant-2", "conversation-1", "receiver-1"));
        });

        var sendResponse = await senderClient.PostAsJsonAsync("/api/conversations/conversation-1/messages", new SendMessageRequest
        {
            Content = "Hello from sender",
            Type = "text"
        });

        Assert.Equal(HttpStatusCode.Created, sendResponse.StatusCode);

        var (sendRaw, message) = await HttpTestHelpers.ReadJsonAsync<MessageDTO>(sendResponse);
        HttpTestHelpers.AssertRawSuccessPayload(sendRaw);
        Assert.Equal("Hello from sender", message.Content);
        Assert.NotNull(sendResponse.Headers.Location);
        Assert.EndsWith(
            $"/api/conversations/conversation-1/messages/{message.Id}",
            sendResponse.Headers.Location!.ToString(),
            StringComparison.Ordinal);

        var getResponse = await senderClient.GetAsync("/api/conversations/conversation-1/messages");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var (getRaw, page) = await HttpTestHelpers.ReadJsonAsync<MessagePageResult>(getResponse);
        HttpTestHelpers.AssertRawSuccessPayload(getRaw);
        Assert.NotNull(page.Messages);
        Assert.Single(page.Messages!);
        Assert.Equal(message.Id, page.Messages![0].Id);

        var markReadResponse = await receiverClient.PutAsJsonAsync(
            "/api/conversations/conversation-1/participants/me/read-state",
            new ReadMessageRequest { LastReadMessageId = message.Id!.Value });

        Assert.Equal(HttpStatusCode.NoContent, markReadResponse.StatusCode);
        await HttpTestHelpers.AssertNoContentAsync(markReadResponse);
    }

    [Fact]
    public async Task GetMessages_ReturnsForbidden_ForOutsider()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient("outsider-1", "outsider@example.com");

        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                TestData.CreateUser("member-1", "member-1@example.com"),
                TestData.CreateUser("member-2", "member-2@example.com"),
                TestData.CreateUser("outsider-1", "outsider@example.com"));
            db.Conversations.Add(TestData.CreateConversation("conversation-1"));
            db.ConversationParticipants.AddRange(
                TestData.CreateConversationParticipant("participant-1", "conversation-1", "member-1"),
                TestData.CreateConversationParticipant("participant-2", "conversation-1", "member-2"));
            db.Messages.Add(new Message
            {
                Id = 10,
                ConversationId = "conversation-1",
                SenderId = "member-1",
                Type = "text",
                CreatedAt = DateTime.UtcNow
            });
            db.MessageDetails.Add(new MessageDetail
            {
                MessageId = 10,
                Content = "Secret"
            });
        });

        var response = await client.GetAsync("/api/conversations/conversation-1/messages");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var error = await HttpTestHelpers.ReadErrorAsync(response);
        Assert.Equal(ErrorCodes.CONVERSATION.USER_NOT_IN_CONVERSATION, error.ErrorCode);
    }
}
