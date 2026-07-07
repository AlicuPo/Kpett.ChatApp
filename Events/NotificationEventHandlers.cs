using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Events.Comment;
using Kpett.ChatApp.Events.Friend;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Hubs;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.DTOs.Response.Notification;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Kpett.ChatApp.Events
{
    public class NotificationEventHandlers :
        INotificationHandler<FriendRequestSentEvent>,
        INotificationHandler<FriendRequestAcceptedEvent>,
        INotificationHandler<CommentMentionedEvent>
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<AppHub> _hubContext;

        public NotificationEventHandlers(AppDbContext context, IHubContext<AppHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // Xử lý Gửi lời mời kết bạn
        public async Task Handle(FriendRequestSentEvent evt, CancellationToken cancel)
        {
            var notification = new Notification
            {
                Id = Guid.NewGuid().ToString(),
                RecipientId = evt.ReceiverId,
                ActorId = evt.SenderId,
                Type = NotificationType.FriendRequestReceived.GetDescription(),
                ReferenceId = evt.RequestId
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync(cancel);

            await PushNotification(notification, cancel);
        }

        // Xử lý Chấp nhận lời mời kết bạn
        public async Task Handle(FriendRequestAcceptedEvent evt, CancellationToken cancel)
        {
            var notification = new Notification
            {
                Id = Guid.NewGuid().ToString(),
                RecipientId = evt.RequesterId,
                ActorId = evt.AccepterId,
                Type = NotificationType.FriendRequestAccepted.GetDescription(),
                ReferenceId = evt.RequestId
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync(cancel);

            await PushNotification(notification, cancel);
        }

        // Xử lý Tag/Mention trong Comment
        public async Task Handle(CommentMentionedEvent evt, CancellationToken cancel)
        {
            var validMentionIds = evt.MentionedUserIds.Where(id => id != evt.ActorId).Distinct().ToList();
            if (!validMentionIds.Any()) return;

            var metadataJson = JsonSerializer.Serialize(new { CommentId = evt.CommentId, TextSnippet = evt.CommentSnippet });

            var notifications = validMentionIds.Select(userId => new Notification
            {
                Id = Guid.NewGuid().ToString(),
                RecipientId = userId,
                ActorId = evt.ActorId,
                Type = NotificationType.CommentMention.GetDescription(),
                ReferenceId = evt.PostId,
                Metadata = metadataJson
            }).ToList();

            await _context.Notifications.AddRangeAsync(notifications, cancel);
            await _context.SaveChangesAsync(cancel);

            foreach (var notif in notifications)
            {
                await PushNotification(notif, cancel);
            }
        }

        // Hàm helper để map dữ liệu Actor và push qua SignalR (DRY)
        private async Task PushNotification(Notification notif, CancellationToken cancel)
        {
            var actorInfo = await _context.Users.AsNoTracking()
                .Where(u => u.Id == notif.ActorId)
                .Select(u => new
                {
                    u.Id,
                    DisplayName = u.DisplayName ?? u.Username,
                    u.Username,
                    AvatarUrl = _context.UserMedias
                        .Where(um => um.UserId == u.Id && um.IsPrimary && um.MediaType == "Avatar")
                        .Select(um => um.MediaUrl)
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync(cancel);

            var payload = new
            {
                notif.Id,
                notif.Type,
                notif.ReferenceId,
                notif.IsRead,
                notif.CreatedAt,
                notif.Metadata,
                Sound = NotificationSoundResponse.FromType(notif.Type),
                Actor = actorInfo
            };

            // Bắn đến chính xác User nhận thông báo
            await _hubContext.Clients.User(notif.RecipientId).SendAsync("ReceiveNotification", payload, cancel);
        }
    }
}
