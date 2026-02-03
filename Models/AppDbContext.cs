using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Kpett.ChatApp.Models;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Block> Blocks { get; set; }

    public virtual DbSet<Comment> Comments { get; set; }

    public virtual DbSet<Conversation> Conversations { get; set; }

    public virtual DbSet<ConversationKey> ConversationKeys { get; set; }

    public virtual DbSet<ConversationParticipant> ConversationParticipants { get; set; }

    public virtual DbSet<Follow> Follows { get; set; }

    public virtual DbSet<FriendRequest> FriendRequests { get; set; }

    public virtual DbSet<Friendship> Friendships { get; set; }

    public virtual DbSet<Group> Groups { get; set; }

    public virtual DbSet<GroupMember> GroupMembers { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<MessageDetail> MessageDetails { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Post> Posts { get; set; }

    public virtual DbSet<PostMedia> PostMedia { get; set; }

    public virtual DbSet<PostReaction> PostReactions { get; set; }

    public virtual DbSet<PostReactionType> PostReactionTypes { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserDevice> UserDevices { get; set; }

    public virtual DbSet<UserFeed> UserFeeds { get; set; }

    public virtual DbSet<UserSetting> UserSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("KpettChatApp");

        //  THIẾT LẬP KHÓA CHÍNH CHO TỪNG BẢNG ---

        // Các bảng dùng khóa đơn chuẩn "Id" (EF Core thường tự nhận, nhưng khai báo lại cho chắc chắn)
        modelBuilder.Entity<User>().HasKey(e => e.Id);
        modelBuilder.Entity<Block>().HasKey(e => e.Id);
        modelBuilder.Entity<Comment>().HasKey(e => e.Id);
        modelBuilder.Entity<Conversation>().HasKey(e => e.Id);
        modelBuilder.Entity<ConversationParticipant>().HasKey(e => e.Id);
        modelBuilder.Entity<Follow>().HasKey(e => e.Id);
        modelBuilder.Entity<FriendRequest>().HasKey(e => e.Id);
        modelBuilder.Entity<Group>().HasKey(e => e.Id);
        modelBuilder.Entity<GroupMember>().HasKey(e => e.Id);
        modelBuilder.Entity<Message>().HasKey(e => e.Id);
        modelBuilder.Entity<Notification>().HasKey(e => e.Id);
        modelBuilder.Entity<Post>().HasKey(e => e.Id);
        modelBuilder.Entity<PostMedia>().HasKey(e => e.Id);
        modelBuilder.Entity<PostReaction>().HasKey(e => e.Id);
        modelBuilder.Entity<PostReactionType>().HasKey(e => e.Id);
        modelBuilder.Entity<UserDevice>().HasKey(e => e.Id);
        modelBuilder.Entity<UserFeed>().HasKey(e => e.Id);
        modelBuilder.Entity<UserSetting>().HasKey(e => e.Id);

        // Các bảng dùng khóa đặc thù hoặc khóa phức hợp (Composite Key)
        modelBuilder.Entity<MessageDetail>().HasKey(e => e.MessageId);

        modelBuilder.Entity<Friendship>().HasKey(e => new { e.UserLowId, e.UserHighId });

        modelBuilder.Entity<ConversationKey>().HasKey(e => new { e.UserLowId, e.UserHighId });

        // XÓA BỎ TẤT CẢ CÁC RÀNG BUỘC QUAN HỆ (FOREIGN KEYS) ---
        // Đoạn này đảm bảo dù Model có thuộc tính điều hướng cũng không tạo FK trong DB
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var foreignKeys = entityType.GetForeignKeys().ToList();
            foreach (var foreignKey in foreignKeys)
            {
                entityType.RemoveForeignKey(foreignKey);
            }
        }
    }
}
