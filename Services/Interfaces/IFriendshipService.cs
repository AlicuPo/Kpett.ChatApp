using Kpett.ChatApp.DTOs;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IFriendshipService
    {
        Task RequestFriendRequestAsync(string senderId, string receiverId, CancellationToken cancel);
        Task AcceptFriendRequestAsync(string senderId, string receiverId, CancellationToken cancel);
        Task RejectFriendRequestAsync(string senderId, string receiverId, CancellationToken cancel);
        Task<List<FriendRequestDTO>> GetPendingFriendRequestsAsync(string userId, CancellationToken cancel);
        Task CancelFriendRequestAsync(string senderId, string receiverId, CancellationToken cancel);
    }
}
