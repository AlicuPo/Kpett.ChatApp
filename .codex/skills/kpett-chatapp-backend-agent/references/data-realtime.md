# Data And Realtime

Use this reference when changing persistence, cache, realtime, background jobs, or tests.

## EF Core

- `AppDbContext` is the central EF Core context.
- Use async EF APIs and pass `CancellationToken`.
- Use `AsNoTracking()` for read-only queries.
- Prefer projection into response DTOs over loading full entity graphs.
- Add migrations only when the model or schema intentionally changes.
- Review existing indexes and query patterns before changing conversation, friend, post, comment, message, or notification queries.

## Redis

- Redis is registered through `IConnectionMultiplexer` in `Program.cs`.
- Use `IRedisService` instead of direct Redis access from controllers or feature code.
- Redis currently supports token blacklist checks, online presence, SignalR connection tracking, conversation access cache, and typing state.
- Keep Redis keys and expirations compatible with existing service methods.

## SignalR

- `AppHub` is the authenticated realtime hub mapped to `/hubs/app`.
- SignalR reads JWT access tokens from `access_token` query string for hub connections.
- Use `IHubContext<AppHub>` from services when pushing events triggered by HTTP workflows.
- Hub methods should validate user identity, check conversation access, join/leave groups, and clean up Redis tracking on disconnect.
- Preserve existing event names used by the frontend, including `NewConversationCreated`, `ReceiveNewMessage`, `UserReadMessage`, `UserStatusChanged`, `UserTyping`, `JoinedConversation`, and `LeftConversation`.

## Hangfire and MediatR

- Hangfire is configured in `Program.cs` with SQL Server storage.
- The recurring `cleanup-temp-images` job calls `IMediaService.CleanUpOrphanedImagesAsync()` daily.
- Use MediatR events in `Events` for cross-cutting domain notifications when the workflow should be decoupled from a controller.

## Tests

- Test project: `Kpett.ChatApp.Tests/Kpett.ChatApp.Tests.csproj`.
- Test infrastructure lives in `Kpett.ChatApp.Tests/Infrastructure`.
- Prefer integration-style tests through `TestWebApplicationFactory` for API contract changes.
- Use focused service tests for business logic, access checks, pagination, presence, and notification behavior.
- Run targeted tests when possible, for example:

```powershell
dotnet test Kpett.ChatApp.Tests/Kpett.ChatApp.Tests.csproj
```
