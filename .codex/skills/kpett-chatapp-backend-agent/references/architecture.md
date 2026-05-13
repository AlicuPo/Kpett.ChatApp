# Architecture

Use this reference for backend structure, layering, and dependency decisions.

## Stack

- ASP.NET Core Web API targeting `net10.0`.
- Entity Framework Core 10 with SQL Server.
- JWT Bearer authentication with access-token blacklist checks in Redis.
- SignalR through `AppHub` at `/hubs/app`.
- StackExchange.Redis and `Microsoft.Extensions.Caching.StackExchangeRedis`.
- Hangfire with SQL Server storage for background jobs.
- MediatR for domain/event-style handlers.
- Cloudinary and R2/media options for media upload workflows.
- xUnit, ASP.NET Core MVC Testing, EF Core InMemory, and SQLite for tests.

## Source layout

- `Program.cs`: service registration, middleware pipeline, authentication, CORS, SignalR, Hangfire, and recurring jobs.
- `Controllers`: API endpoints and HTTP response shaping.
- `Services/Interfaces`: service contracts.
- `Services/Impls`: business logic and persistence orchestration.
- `DTOs/Request`: request bodies and query parameter models.
- `DTOs/Response`: response DTOs and shared response wrappers.
- `DTOs/Payload/Cursor`: cursor payloads encoded by `CursorHelper`.
- `Models`: EF entities and `AppDbContext`.
- `Hubs`: SignalR hub methods and connection lifecycle.
- `Events`: MediatR notifications and handlers.
- `Exceptions`: custom exception types and global exception handler.
- `Constants`: shared error codes and fixed protocol strings.
- `Options`: configuration-bound options.
- `Kpett.ChatApp.Tests`: xUnit tests and test infrastructure.

## Layering rules

- Controllers should stay thin: read claims, bind route/query/body data, call services, and return `GeneralResponse` wrappers.
- Services should own validation, authorization checks, EF queries, Redis/cache coordination, SignalR pushes, and domain workflows.
- Add new service behavior to both `Services/Interfaces` and `Services/Impls`, then register new services in `Program.cs` if needed.
- Keep reusable user/claims/cursor/date helpers in `Helper` or `Extensions` rather than duplicating logic.
- Use existing enum description helpers and constants for protocol values that are shared with the frontend.

## Configuration

- Use options classes in `Options` for configuration sections when adding new config.
- Do not commit real secrets. Keep shareable defaults in `appsettings.Example.json`.
- Preserve CORS policy name `ClientCors` and frontend-compatible `AllowCredentials`.
- Preserve `/api/[controller]` route shape unless the frontend contract is intentionally changed.
