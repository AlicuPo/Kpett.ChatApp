namespace Kpett.ChatApp.Services.Abstractions
{
    /// <summary>
    /// Service thao tác với Redis: token, OTP, online presence, typing tracking, conversation membership cache.
    /// </summary>
    public interface IRedisService
    {
        /// <summary>Lưu refresh token với TTL.</summary>
        Task SaveRefreshTokenAsync(string userId, string refreshToken, TimeSpan ttl);
        /// <summary>Lấy refresh token của người dùng.</summary>
        Task<string?> GetRefreshTokenAsync(string userId);
        /// <summary>Xoá refresh token.</summary>
        Task RemoveRefreshTokenAsync(string userId);

        /// <summary>Thêm access token JTI vào blacklist.</summary>
        Task BlacklistAccessTokenAsync(string jti, TimeSpan ttl);
        /// <summary>Kiểm tra access token có trong blacklist không.</summary>
        Task<bool> IsAccessTokenBlacklistedAsync(string jti);

        /// <summary>Thêm refresh token vào blacklist.</summary>
        Task BlacklistRefreshTokenAsync(string refreshToken, TimeSpan ttl);
        /// <summary>Kiểm tra refresh token có trong blacklist không.</summary>
        Task<bool> IsRefreshTokenBlacklistedAsync(string refreshToken);

        /// <summary>Lưu OTP đặt lại mật khẩu với TTL.</summary>
        Task SavePasswordResetOtpAsync(string email, string otp, TimeSpan ttl);
        /// <summary>Lấy OTP đặt lại mật khẩu.</summary>
        Task<string?> GetPasswordResetOtpAsync(string email);
        /// <summary>Xoá OTP đặt lại mật khẩu.</summary>
        Task RemovePasswordResetOtpAsync(string email);

        /// <summary>Lưu connectionId của user (online presence).</summary>
        Task AddConnectionAsync(string userId, string connectionId);
        /// <summary>Xoá connectionId của user.</summary>
        Task RemoveConnectionAsync(string userId, string connectionId);
        /// <summary>Lấy danh sách connectionId của user.</summary>
        Task<string[]> GetConnectionsAsync(string userId);
        /// <summary>Kiểm tra user có đang online không.</summary>
        Task<bool> IsUserOnlineAsync(string userId);
        /// <summary>Lấy trạng thái online của nhiều user.</summary>
        Task<Dictionary<string, bool>> GetUsersOnlineStatusAsync(IEnumerable<string> userIds);

        /// <summary>Thêm user vào danh sách thành viên conversation (cache).</summary>
        Task AddUserToConversationAsync(string conversationId, string userId);
        /// <summary>Xoá user khỏi danh sách thành viên conversation (cache).</summary>
        Task RemoveUserFromConversationAsync(string conversationId, string userId);
        /// <summary>Lấy danh sách user trong conversation (cache).</summary>
        Task<string[]> GetConversationUsersAsync(string conversationId);
        /// <summary>Gán connectionId cho conversation (cho SignalR).</summary>
        Task TrackConnectionConversationAsync(string connectionId, string conversationId);
        /// <summary>Bỏ gán connectionId khỏi conversation.</summary>
        Task UntrackConnectionConversationAsync(string connectionId, string conversationId);
        /// <summary>Lấy danh sách conversation của connection.</summary>
        Task<string[]> GetConnectionConversationsAsync(string connectionId);
        /// <summary>Xoá tất cả conversation tracking của connection.</summary>
        Task ClearConnectionConversationsAsync(string connectionId);

        /// <summary>Publish message lên Redis channel.</summary>
        Task<long> PublishAsync(string channel, string message);

        /// <summary>Cache quyền truy cập conversation (dùng cho typing indicator).</summary>
        Task SetConversationAccessCacheAsync(string userId, string conversationId, TimeSpan ttl);
        /// <summary>Kiểm tra cache quyền truy cập conversation.</summary>
        Task<bool> GetConversationAccessCacheAsync(string userId, string conversationId);

        /// <summary>Đặt trạng thái typing cho 1 connection. Score = expiry Unix timestamp.</summary>
        Task SetUserTypingAsync(string conversationId, string userId, string connectionId, TimeSpan ttl);
        /// <summary>Kiểm tra 1 connection cụ thể có đang typing không.</summary>
        Task<bool> IsUserConnectionTypingAsync(string conversationId, string userId, string connectionId);
        /// <summary>Xoá trạng thái typing của 1 connection.</summary>
        Task RemoveUserTypingAsync(string conversationId, string userId, string connectionId);
        /// <summary>Kiểm tra user có connection khác đang typing trong conversation không (multi-tab).</summary>
        Task<bool> HasOtherTypingConnectionsAsync(string conversationId, string userId, string excludeConnectionId);
        /// <summary>Lấy danh sách (userId, connectionId) đang typing trong conversation.</summary>
        Task<List<(string UserId, string ConnectionId)>> GetTypingUsersInConversationAsync(string conversationId);
        /// <summary>Xoá tất cả typing state của 1 connectionId khi disconnect.</summary>
        Task<List<(string ConversationId, string UserId)>> RemoveAllTypingForConnectionAsync(string connectionId);
    }
}


