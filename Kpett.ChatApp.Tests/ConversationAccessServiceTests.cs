using Kpett.ChatApp.Contants;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Impls;
using Microsoft.EntityFrameworkCore;

namespace Kpett.ChatApp.Tests;

public class ConversationAccessServiceTests
{
    [Fact]
    public async Task EnsureCanAccessConversationAsync_AllowsParticipant()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(new Conversation { Id = "conversation-1" });
        dbContext.ConversationParticipants.Add(new ConversationParticipant
        {
            Id = "participant-1",
            ConversationId = "conversation-1",
            UserId = "user-1"
        });
        await dbContext.SaveChangesAsync();

        var service = new ConversationAccessService(dbContext);

        await service.EnsureCanAccessConversationAsync("conversation-1", "user-1", CancellationToken.None);
    }

    [Fact]
    public async Task EnsureCanAccessConversationAsync_ThrowsNotFound_WhenConversationMissing()
    {
        await using var dbContext = CreateDbContext();
        var service = new ConversationAccessService(dbContext);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            service.EnsureCanAccessConversationAsync("missing-conversation", "user-1", CancellationToken.None));

        Assert.Equal(ErrorCodes.CONVERSATION.NOT_FOUND, exception.ErrorCode);
    }

    [Fact]
    public async Task EnsureCanAccessConversationAsync_ThrowsForbidden_WhenUserIsNotParticipant()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Conversations.Add(new Conversation { Id = "conversation-1" });
        await dbContext.SaveChangesAsync();

        var service = new ConversationAccessService(dbContext);

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.EnsureCanAccessConversationAsync("conversation-1", "user-2", CancellationToken.None));

        Assert.Equal(ErrorCodes.CONVERSATION.USER_NOT_IN_CONVERSATION, exception.ErrorCode);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
