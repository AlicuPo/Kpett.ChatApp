using System.Text.Json;
using Kpett.ChatApp.DTOs;
using Kpett.ChatApp.DTOs.Response.Message;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Impls;
using Microsoft.EntityFrameworkCore;

namespace Kpett.ChatApp.Tests;

public class NotificationServiceTests
{
    [Fact]
    public async Task CreateMessageNotificationsAsync_CreatesNotificationsForAllRecipientsExceptSender()
    {
        await using var dbContext = CreateDbContext();
        dbContext.ConversationParticipants.AddRange(
            new ConversationParticipant
            {
                Id = "participant-1",
                ConversationId = "conversation-1",
                UserId = "sender-1"
            },
            new ConversationParticipant
            {
                Id = "participant-2",
                ConversationId = "conversation-1",
                UserId = "receiver-1"
            },
            new ConversationParticipant
            {
                Id = "participant-3",
                ConversationId = "conversation-1",
                UserId = "receiver-2"
            });
        await dbContext.SaveChangesAsync();

        var service = new NotificationService(dbContext);
        var dto = new MessageDTO
        {
            Id = 123,
            Content = "hello world"
        };

        await service.CreateMessageNotificationsAsync("conversation-1", "sender-1", dto);

        var notifications = await dbContext.Notifications
            .OrderBy(n => n.UserId)
            .ToListAsync();

        Assert.Equal(2, notifications.Count);
        Assert.DoesNotContain(notifications, notification => notification.UserId == "sender-1");

        foreach (var notification in notifications)
        {
            Assert.False(string.IsNullOrWhiteSpace(notification.Id));
            Assert.Equal("sender-1", notification.SenderId);
            Assert.Equal("MESSAGE", notification.Type);
            Assert.Equal("hello world", notification.Content);
            Assert.False(notification.IsRead ?? true);
            Assert.NotNull(notification.CreatedAt);

            using var document = JsonDocument.Parse(notification.Data!);
            var root = document.RootElement;
            Assert.Equal("conversation-1", root.GetProperty("conversationId").GetString());
            Assert.Equal(123, root.GetProperty("messageId").GetInt64());
        }
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
