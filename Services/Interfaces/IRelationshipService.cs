using Kpett.ChatApp.DTOs.Request.Friend;
using Kpett.ChatApp.DTOs.Response.Friend;
using Kpett.ChatApp.DTOs.Response.Shared;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IRelationshipService
    {
        Task<FriendRequestResponse> SendFriendRequestAsync(string senderId, string receiverId);
        Task AcceptFriendRequestAsync(string senderId, string receiverId);
        Task DeclineFriendRequestAsync(string senderId, string receiverId);
        Task CancelFriendRequestAsync(string senderId, string receiverId);
        Task UnfriendAsync(string currentUserId, string targetUserId);
        Task FollowAsync(string followerId, string followeeId);
        Task UnfollowAsync(string followerId, string followeeId);
        Task<PaginatedData<FriendListItemDTO>> GetFriendsAsync(string currentUserId, FriendListRequest request, CancellationToken cancel);
    }
}
