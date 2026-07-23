using Kpett.ChatApp.DTOs.Request.Friend;
using Kpett.ChatApp.DTOs.Response.Friend;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.DTOs.Response.User;

namespace Kpett.ChatApp.Services.Abstractions
{
    /// <summary>
    /// Service quản lý quan hệ bạn bè: kết bạn, huỷ kết bạn, theo dõi, đề xuất bạn bè.
    /// </summary>
    public interface IRelationshipService
    {
        /// <summary>Gửi lời mời kết bạn.</summary>
        Task<FriendRequestResponse> SendFriendRequestAsync(string senderId, string receiverId);

        /// <summary>Chấp nhận lời mời kết bạn.</summary>
        Task AcceptFriendRequestAsync(string currentUserId, string requestId);

        /// <summary>Từ chối lời mời kết bạn.</summary>
        Task DeclineFriendRequestAsync(string currentUserId, string requestId);

        /// <summary>Huỷ lời mời kết bạn đã gửi.</summary>
        Task CancelFriendRequestAsync(string currentUserId, string requestId);

        /// <summary>Huỷ kết bạn.</summary>
        Task UnfriendAsync(string currentUserId, string targetUserId);

        /// <summary>Theo dõi người dùng.</summary>
        Task FollowAsync(string followerId, string followeeId);

        /// <summary>Bỏ theo dõi người dùng.</summary>
        Task UnfollowAsync(string followerId, string followeeId);

        /// <summary>Lấy danh sách bạn bè (phân trang).</summary>
        Task<PaginatedData<FriendListItemDTO>> GetFriendsAsync(string currentUserId, FriendListRequest request, CancellationToken cancel);

        /// <summary>Lấy bạn bè chưa tham gia nhóm (phân trang).</summary>
        Task<PaginatedData<UserResponse>> GetFriendsNotInGroupAsync(string currentUserId, GetFriendsNotInGroupRequest request, CancellationToken cancel);

        /// <summary>Lấy danh sách gợi ý kết bạn.</summary>
        Task<List<UserResponse>> GetFriendSuggestionsAsync(string currentUserId, int limit, CancellationToken cancel);
    }
}


