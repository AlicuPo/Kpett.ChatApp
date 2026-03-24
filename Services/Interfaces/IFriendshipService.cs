using Kpett.ChatApp.DTOs.Response.Friend;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IFriendshipService
    {
        Task<FriendRequestDTO> CreateFriendRequestAsync(string senderId, string receiverId, CancellationToken cancel);
        Task<FriendRequestDTO> UpdateFriendRequestStatusAsync(string friendRequestId, string currentUserId, string status, CancellationToken cancel);
        Task<List<FriendRequestDTO>> GetPendingFriendRequestsAsync(string userId, CancellationToken cancel);
        Task CancelFriendRequestAsync(string friendRequestId, string currentUserId, CancellationToken cancel);
    }
}
