using Kpett.ChatApp.DTOs.Request.Post;
using Kpett.ChatApp.DTOs.Request.User;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.DTOs.Response.User;

namespace Kpett.ChatApp.Services.Abstractions
{
    /// <summary>
    /// Service quản lý người dùng: thông tin cá nhân, media, tìm kiếm, thiết lập tài khoản.
    /// </summary>
    public interface IUserService
    {
        /// <summary>Kiểm tra username đã tồn tại chưa.</summary>
        Task<UsernameCheckResponse> CheckExistByUsernameAsync(string username, CancellationToken cancel);

        /// <summary>Lấy danh sách tất cả người dùng (phân trang, tìm kiếm).</summary>
        Task<(List<UserResponse>, int)> GetAllUserAsync(UserRequest search, CancellationToken cancel = default);

        /// <summary>Lấy thông tin cá nhân của người dùng hiện tại.</summary>
        Task<UserGeneralInfoResponse> GetMyGeneralInfoAsync(string userId, CancellationToken cancel);

        /// <summary>Cập nhật thông tin cá nhân.</summary>
        Task<UserGeneralInfoResponse> UpdateUserGeneralInfoAsync(string currentUserId, UpdateGeneralInfoUserRequest request, CancellationToken cancel);

        /// <summary>Cập nhật media (avatar/cover).</summary>
        Task<UserMediaResponse> UpdateUserMediaAsync(string currentUserId, MediaRequest media, string mediaType);

        /// <summary>Xoá media chính (avatar/cover).</summary>
        Task<bool> DeleteUserMediaPrimaryAsync(string currentUserId, string mediaType);

        /// <summary>Xoá tài khoản người dùng.</summary>
        Task<bool> DeleteUserAsync(string id, string currentUserId, CancellationToken cancel);

        /// <summary>Thiết lập tài khoản lần đầu.</summary>
        Task<UserResponse> AccountSetupAsync(string userId, AccountSetupRequest accountSetupRequest, CancellationToken cancel);

        /// <summary>Lấy thống kê của người dùng.</summary>
        Task<UserWithStatResponse> GetUserStatsAsync(string userId, CancellationToken cancel);

        /// <summary>Lấy hồ sơ người dùng theo username.</summary>
        Task<UserProfileResponse> GetUserProfileAsync(string targetUsername, string? currentUserId, CancellationToken cancel);

        /// <summary>Tìm kiếm người dùng theo từ khoá (cursor pagination).</summary>
        Task<PaginatedData<UserResponse>> SearchUsersAsync(string? currentUserId, string keyword, int limit, string? cursor, CancellationToken cancel);
    }
}


