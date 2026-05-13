# API Contracts

Use this reference when changing controllers, DTOs, response shapes, auth behavior, or frontend-facing contracts.

## Response wrappers

Shared response records live in `DTOs/Response/Shared/GeneralResponse.cs`:

```csharp
public record GeneralResponse
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public int StatusCode { get; set; }
}

public record ErrorResponse : GeneralResponse
{
    public string ErrorCode { get; init; } = string.Empty;
    public string? StackTrace { get; init; }
}

public record GeneralResponse<T> : GeneralResponse
{
    public T Data { get; init; } = default!;
}
```

Cursor pagination uses `PaginatedData<T>` with `Items` and `Pagination`. The frontend expects camelCase JSON, so preserve property names through normal ASP.NET Core JSON serialization.

## Controllers

- Use `[ApiController]`, `[Route("api/[controller]")]`, and `[Authorize]` consistently for authenticated resources.
- Use `User.GetRequiredUserId()` for authenticated endpoints unless an existing endpoint has a specific reason to handle missing claims manually.
- Return `Ok`, `Created`, or `StatusCode` with `GeneralResponse` or `GeneralResponse<T>`.
- Bind cursor/list filters from query DTOs such as `CursorPaginationRequest`, `ConversationListRequest`, or domain-specific request types.
- Pass `CancellationToken` from controller actions into services.

## Exceptions and error codes

- Throw custom exceptions from `Exceptions` for expected failures: `BadRequestException`, `UnauthorizedException`, `ForbiddenException`, `NotFoundException`, and `ConflictException`.
- Use `Constants/ErrorCodes.cs` instead of ad hoc error-code strings.
- Let `GlobalExceptionHandler` convert `AppException` instances into `ErrorResponse`.
- Preserve auth challenge behavior in `Program.cs` for invalid/expired access tokens because the frontend refresh flow depends on `AUTH.ACCESS_TOKEN_INVALID`.

## DTOs and wire compatibility

- Add request DTOs under `DTOs/Request/<Domain>`.
- Add response DTOs under `DTOs/Response/<Domain>`.
- Keep DTOs explicit; do not return EF entities from controllers.
- Keep frontend field expectations stable for conversation, message, notification, post, user, media, and relationship responses.
- When adding enum-like wire values, check existing `Enums`, `Constants`, and frontend types before choosing names.

## Cursor pagination

- Cursor payload records live in `DTOs/Payload/Cursor`.
- Use `CursorHelper.Encode` and `CursorHelper.Decode<T>()`.
- Query `limit + 1` records to compute `NextCursor`, then remove the extra record.
- Clamp limits in services to existing domain defaults and maximums.
- Keep ordering deterministic with a tie-breaker such as ID when paginating by time.
