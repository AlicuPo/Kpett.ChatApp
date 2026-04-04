using Kpett.ChatApp.DTOs.Request.Conversation;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Impls;
using Kpett.ChatApp.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kpett.ChatApp.Tests;

public class ConversationServiceRelationalTests
{
    [Fact]
    public async Task CreateConversationAsync_ReusesExistingConversation_WhenUniqueKeyRaceOccurs()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"kpett-chat-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";

        try
        {
            var baseOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connectionString)
                .Options;

            await using (var setupContext = new AppDbContext(baseOptions))
            {
                await setupContext.Database.EnsureDeletedAsync();
                await setupContext.Database.EnsureCreatedAsync();

                setupContext.Users.AddRange(
                    TestData.CreateUser("user-1", "user-1@example.com"),
                    TestData.CreateUser("user-2", "user-2@example.com"));
                setupContext.Friendships.Add(TestData.CreateAcceptedFriendship("user-1", "user-2"));
                await setupContext.SaveChangesAsync();
            }

            var raceInterceptor = new ConversationRaceInterceptor(connectionString);
            var serviceOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connectionString)
                .AddInterceptors(raceInterceptor)
                .Options;

            await using var serviceContext = new AppDbContext(serviceOptions);
            var service = new ConversationService(serviceContext);

            var result = await service.CreateConversationAsync(
                "user-1",
                new ConversationKeysRequest
                {
                    UserLow = "user-1",
                    UserHigh = "user-2",
                    Type = "direct",
                    Name = "Requested conversation"
                },
                CancellationToken.None);

            Assert.False(result.IsCreated);
            Assert.Equal("existing-conversation", result.Conversation.Id);
            Assert.Equal("Competing conversation", result.Conversation.Name);

            await using var verificationContext = new AppDbContext(baseOptions);
            Assert.Single(await verificationContext.Conversations.ToListAsync());
            Assert.Single(await verificationContext.ConversationKeys.ToListAsync());
            Assert.Equal(2, await verificationContext.ConversationParticipants.CountAsync());
        }
        finally
        {
            try
            {
                if (File.Exists(databasePath))
                {
                    File.Delete(databasePath);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    private sealed class ConversationRaceInterceptor : SaveChangesInterceptor
    {
        private readonly string _connectionString;
        private int _hasInjected;

        public ConversationRaceInterceptor(string connectionString)
        {
            _connectionString = connectionString;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            await InjectCompetingConversationAsync(eventData.Context, cancellationToken);
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private async Task InjectCompetingConversationAsync(DbContext? currentContext, CancellationToken cancellationToken)
        {
            if (currentContext == null || Interlocked.Exchange(ref _hasInjected, 1) == 1)
            {
                return;
            }

            var pendingConversationKey = currentContext.ChangeTracker
                .Entries<ConversationKey>()
                .Select(entry => entry.Entity)
                .SingleOrDefault();

            if (pendingConversationKey == null)
            {
                return;
            }

            var competingOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connectionString)
                .Options;

            await using var competingContext = new AppDbContext(competingOptions);
            competingContext.Conversations.Add(new Conversation
            {
                Id = "existing-conversation",
                Type = "direct",
                Name = "Competing conversation",
                CreatedAt = DateTime.UtcNow,
                LastMessageAt = DateTime.UtcNow,
                IsActive = true
            });
            competingContext.ConversationKeys.Add(new ConversationKey
            {
                Id = Guid.NewGuid().ToString(),
                ConversationId = "existing-conversation",
                UserLowId = pendingConversationKey.UserLowId,
                UserHighId = pendingConversationKey.UserHighId
            });
            competingContext.ConversationParticipants.AddRange(
                TestData.CreateConversationParticipant("existing-participant-1", "existing-conversation", pendingConversationKey.UserLowId!),
                TestData.CreateConversationParticipant("existing-participant-2", "existing-conversation", pendingConversationKey.UserHighId!));

            await competingContext.SaveChangesAsync(cancellationToken);
        }
    }
}
