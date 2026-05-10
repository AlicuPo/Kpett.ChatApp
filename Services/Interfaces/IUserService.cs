using Kpett.ChatApp.DTOs.Request.Post;
using Kpett.ChatApp.DTOs.Request.User;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.DTOs.Response.User;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IUserService
    {
        Task<UsernameCheckResponse> CheckExistByUsername(string username, CancellationToken cancel);
        Task<(List<UserResponse>, int)> GetAllUser(UserRequest search, CancellationToken cancel = default);
        Task<UserGeneralInfoResponse> GetMyGeneralInfo(string userId, CancellationToken cancel);
        Task<UserGeneralInfoResponse> UpdateUserGeneralInfo(string currentUserId, UpdateGeneralInfoUserRequest request, CancellationToken cancel);
        Task<UserMediaResponse> UpdateUserMedia(string currentUserId, MediaRequest media, string mediaType);
        Task<bool> DeleteUserMediaPrimaryAsync(string currentUserId, string mediaType);
        Task<bool> DeleteUser(string id, string currentUserId, CancellationToken cancel);
        Task<UserResponse> AccountSetup(string userId, AccountSetupRequest accountSetupRequest, CancellationToken cancel);
        Task<UserWithStatResponse> GetUserStatsAsync(string userId, CancellationToken cancel);
        Task<UserProfileResponse> GetUserProfileAsync(string targetUsername, string? currentUserId, CancellationToken cancel);
        Task<PaginatedData<UserResponse>> SearchUsersAsync(string currentUserId, string keyword, int limit, string? cursor, CancellationToken cancel);
    }
}
