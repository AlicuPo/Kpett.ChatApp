using Kpett.ChatApp.DTOs.Response.Conversation;
using Kpett.ChatApp.Hubs;
using Kpett.ChatApp.Services.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace Kpett.ChatApp.Services.Implementations;

public class TypingExpirationWorker : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(2);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<AppHub> _hubContext;
    private readonly ILogger<TypingExpirationWorker> _logger;

    public TypingExpirationWorker(
        IServiceScopeFactory scopeFactory,
        IHubContext<AppHub> hubContext,
        ILogger<TypingExpirationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(ScanInterval);

        while (await WaitForNextTickAsync(timer, stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var redis = scope.ServiceProvider.GetRequiredService<IRedisService>();
                var expiredEntries = await redis.RemoveExpiredTypingAsync();

                foreach (var expiredUser in expiredEntries.GroupBy(e => new { e.ConversationId, e.UserId }))
                {
                    var sample = expiredUser.First();
                    var hasOtherTypingConnection = await redis.HasOtherTypingConnectionsAsync(
                        sample.ConversationId,
                        sample.UserId,
                        sample.ConnectionId);

                    if (hasOtherTypingConnection)
                    {
                        continue;
                    }

                    await _hubContext.Clients.Group($"conversation_{sample.ConversationId}")
                        .SendAsync(
                            "UserTyping",
                            new TypingEventPayload
                            {
                                UserId = sample.UserId,
                                ConversationId = sample.ConversationId,
                                IsTyping = false,
                                Timestamp = DateTime.UtcNow
                            },
                            stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error expiring typing indicators");
            }
        }
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
