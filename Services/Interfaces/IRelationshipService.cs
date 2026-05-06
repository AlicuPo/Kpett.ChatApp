using Kpett.ChatApp.DTOs.Request.Firend;
using Kpett.ChatApp.DTOs.Request.Friend;
using Kpett.ChatApp.DTOs.Response.Friend;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.DTOs.Response.User;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IRelationshipService
    {
        Task<FriendRequestResponse> SendFriendRequestAsync(string senderId, string receiverId);
        Task AcceptFriendRequestAsync(string currentUserId, string requestId);
        Task DeclineFriendRequestAsync(string currentUserId, string requestId);
        Task CancelFriendRequestAsync(string currentUserId, string requestId);
        Task UnfriendAsync(string currentUserId, string targetUserId);
        Task FollowAsync(string followerId, string followeeId);
        Task UnfollowAsync(string followerId, string followeeId);
        Task<PaginatedData<FriendListItemDTO>> GetFriendsAsync(string currentUserId, FriendListRequest request, CancellationToken cancel);
        Task<PaginatedData<UserResponse>> GetFriendsNotInGroupAsync(string currentUserId, GetFriendsNotInGroupRequest request, CancellationToken cancel);
    }
}
