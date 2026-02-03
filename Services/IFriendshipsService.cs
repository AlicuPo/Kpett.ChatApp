using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Microsoft.EntityFrameworkCore;

namespace Kpett.ChatApp.Services
{
    public interface IFriendshipsService
    {
        Task RequestFriendRequestAsync(string senderId, string receiverId, CancellationToken cancel);
        Task AcceptFriendRequestAsync(string senderId, string receiverId, CancellationToken cancel);
    }

    public class FriendshipsServicesImpl : IFriendshipsService
    {
        private readonly AppDbContext _dbcontext;

        public FriendshipsServicesImpl(AppDbContext dbcontext)
        {
            _dbcontext = dbcontext;
        }

        public async Task RequestFriendRequestAsync(string senderId, string receiverId, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(senderId) || string.IsNullOrWhiteSpace(receiverId))
            {
                throw new AppException(StatusCodes.Status400BadRequest, "SenderId or ReceiverId cannot be empty");
            }
            if (senderId == receiverId)
            {
                throw new AppException(StatusCodes.Status400BadRequest, "Cannot accept friend request from yourself");
            }

            cancel.ThrowIfCancellationRequested();

            var isBlocked = await _dbcontext.Blocks.AnyAsync(_ => (_.BlockerId == senderId && _.BlockedId == receiverId) || (_.BlockerId == receiverId && _.BlockedId == senderId), cancel);
            if (isBlocked)
                throw new AppException(StatusCodes.Status403Forbidden, "Cannot send friend request");

            string lowId = string.CompareOrdinal(senderId, receiverId) < 0 ? senderId : receiverId;
            string highId = string.CompareOrdinal(senderId, receiverId) < 0 ? receiverId : senderId;

            var isFriend = await _dbcontext.Friendships.AnyAsync(_ => _.UserLowId == lowId && _.UserHighId == highId && _.Status == EnumHelper.GetDescription(FriendshipsEnums.Accepted), cancel);
            if (isFriend)
                throw new AppException(StatusCodes.Status409Conflict, "Already friends");


            //var existingRequest = await _dbcontext.FriendRequests.FirstOrDefaultAsync(_ => (_.SenderId == senderId && _.ReceiverId == receiverId) || (_.SenderId == receiverId && _.ReceiverId == senderId), cancel);

            //if (existingRequest == null)
            //    throw new AppException(StatusCodes.Status404NotFound, "No friend request found");


            var request = new FriendRequest
            {
                Id = Guid.NewGuid().ToString(),
                SenderId = senderId,
                ReceiverId = receiverId,
                Status = EnumHelper.GetDescription(FriendshipsEnums.Pending),
                CreatedAt = DateTime.UtcNow
            };

            _dbcontext.FriendRequests.Add(request);
            await _dbcontext.SaveChangesAsync(cancel);
            // có thể gửi thông báo cho người gửi ở đây (nếu cần) Signalr
        }



        public async Task AcceptFriendRequestAsync(string senderId, string receiverId, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            using var tx = await _dbcontext.Database.BeginTransactionAsync(cancel);


            var request = await _dbcontext.FriendRequests.FirstOrDefaultAsync(_ => _.SenderId == senderId && _.ReceiverId == receiverId && _.Status == EnumHelper.GetDescription(FriendshipsEnums.Pending), cancel);

            if (request == null)
                throw new AppException(404, "Friend request not found");

            var lowId = senderId.CompareTo(receiverId) < 0 ? senderId : receiverId;
            var highId = senderId.CompareTo(receiverId) < 0 ? receiverId : senderId;

            var friendship = new Friendship
            {
                UserLowId = lowId,
                UserHighId = highId,
                Status = EnumHelper.GetDescription(FriendshipsEnums.Accepted),
                ActionUserId = receiverId,
                CreatedAt = DateTime.UtcNow
            };

            _dbcontext.Friendships.Add(friendship);

            request.Status = EnumHelper.GetDescription(FriendshipsEnums.Accepted);
            request.UpdatedAt = DateTime.UtcNow;

            await _dbcontext.SaveChangesAsync(cancel);
            await tx.CommitAsync(cancel);
            // có thể gửi thông báo cho người gửi ở đây (nếu cần) Signalr
        }



    }


}
