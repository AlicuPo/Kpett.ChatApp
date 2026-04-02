using Kpett.ChatApp.DTOs.Request.Friend;
using Kpett.ChatApp.DTOs.Response.Friend;
using Kpett.ChatApp.DTOs.Response.Shared;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IFriendshipService
    {
        Task<CreateFriendRequestResult> CreateFriendRequestAsync(string senderId, string receiverId, CancellationToken cancel);
        Task<FriendRequestDTO> UpdateFriendRequestStatusAsync(string friendRequestId, string currentUserId, string status, CancellationToken cancel);
        Task<List<FriendRequestDTO>> GetPendingFriendRequestsAsync(string userId, CancellationToken cancel);
        Task<PaginatedData<FriendListItemDTO>> GetFriendsAsync(string currentUserId, FriendListRequest request, CancellationToken cancel);
        Task CancelFriendRequestAsync(string friendRequestId, string currentUserId, CancellationToken cancel);
    }
}
