---
name: kpett-chatapp-backend-agent
description: Build, modify, review, and debug the Kpett ChatApp backend. Use when Codex works on ASP.NET Core controllers, services, DTOs, EF Core models, migrations, auth, JWT refresh and blacklist behavior, Redis, SignalR hubs, Hangfire jobs, MediatR events, media uploads, social graph features, conversations, messages, notifications, posts, comments, error contracts, or xUnit backend tests in this project.
---

# Kpett ChatApp Backend Agent

Use this skill when working in the Kpett ChatApp backend repository.

## Core workflow

1. Inspect the relevant controller, service interface, service implementation, DTOs, model mappings, and tests before changing code.
2. Preserve the layered shape: controllers handle HTTP and response wrappers, services hold business logic, DTOs define wire contracts, models define EF entities, and helpers/extensions hold shared utilities.
3. Keep API responses aligned with `GeneralResponse`, `GeneralResponse<T>`, `ErrorResponse`, `PaginatedData<T>`, and `CursorPaginationMeta`.
4. Keep authentication, authorization, realtime, and cursor pagination behavior compatible with the frontend contract.
5. Verify with the narrowest meaningful `dotnet test` command when possible.

## References

Load only the reference needed for the task:

- `references/architecture.md`: project layout, dependency registration, layering, and configuration rules.
- `references/api-contracts.md`: controllers, DTOs, response wrappers, exceptions, auth, and cursor pagination.
- `references/data-realtime.md`: EF Core, Redis, SignalR, Hangfire, MediatR, and test patterns.

## Default rules

- Add or update service interfaces and implementations together.
- Use existing custom exceptions and `ErrorCodes` for expected failures.
- Use `User.GetRequiredUserId()` or existing claims helpers for authenticated user IDs.
- Keep EF queries cancellation-aware and pass `CancellationToken`.
- Prefer projection DTOs and `AsNoTracking()` for read endpoints.
- Do not bypass `GlobalExceptionHandler` with ad hoc error JSON unless the existing endpoint pattern requires it.
- Do not commit secrets from `appsettings.Development.json`; use example/config patterns for shareable settings.
