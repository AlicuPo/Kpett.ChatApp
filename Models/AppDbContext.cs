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

    public virtual DbSet<PostMedium> PostMedia { get; set; }

    public virtual DbSet<PostReaction> PostReactions { get; set; }

    public virtual DbSet<PostReactionType> PostReactionTypes { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserDevice> UserDevices { get; set; }

    public virtual DbSet<UserFeed> UserFeeds { get; set; }

    public virtual DbSet<UserSetting> UserSettings { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=DU\\SQLEXPRESS;Database=Kpett_ChatApp;Trusted_Connection=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Block>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Blocks__3214EC07B166B0CA");

            entity.Property(e => e.Id)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.BlockedId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.BlockerId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.Reason).HasMaxLength(500);
        });

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Comments__3214EC07CCC91309");

            entity.Property(e => e.Id)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Content).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.DeletedAt).HasColumnType("datetime");
            entity.Property(e => e.ParentCommentId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
            entity.Property(e => e.UserId)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Conversa__3214EC079A36F026");

            entity.Property(e => e.Id)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.CreatedByUserId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LastMessageAt).HasColumnType("datetime");
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<ConversationKey>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Conversa__3214EC07D72CFEC1");

            entity.HasIndex(e => new { e.UserLowId, e.UserHighId }, "UQ_ConversationKeys").IsUnique();

            entity.Property(e => e.Id)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ConversationId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UserHighId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UserLowId)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ConversationParticipant>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Conversa__3214EC07121042EF");

            entity.Property(e => e.Id)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ConversationId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IsArchived).HasDefaultValue(false);
            entity.Property(e => e.IsMuted).HasDefaultValue(false);
            entity.Property(e => e.JoinedAt).HasColumnType("datetime");
            entity.Property(e => e.LastReadAt).HasColumnType("datetime");
            entity.Property(e => e.Role)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UserId)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Follow>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Follows__3214EC07D13FF713");

            entity.Property(e => e.Id)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.FollowerId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.FollowingId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IsMuted).HasDefaultValue(false);
        });

        modelBuilder.Entity<FriendRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__FriendRe__3214EC0732A9F75B");

            entity.Property(e => e.Id)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.Message).HasMaxLength(500);
            entity.Property(e => e.ReceiverId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SenderId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pending");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<Friendship>(entity =>
        {
            entity.HasKey(e => new { e.UserLowId, e.UserHighId });

            entity.Property(e => e.UserLowId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UserHighId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ActionUserId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<Group>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Groups__3214EC073F0DC328");

            entity.Property(e => e.Id)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.AvatarUrl).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.CreatedByUserId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
            entity.Property(e => e.UpdatedByUserId)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<GroupMember>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__GroupMem__3214EC0771B25A22");

            entity.Property(e => e.Id)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.CreatedByUserId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.GroupId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.JoinedAt).HasColumnType("datetime");
            entity.Property(e => e.Role)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
            entity.Property(e => e.UpdatedByUserId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UserId)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Messages__3214EC0781B73AAF");

            entity.HasIndex(e => new { e.ConversationId, e.CreatedAt }, "IDX_Messages_Conversation_Created").IsDescending(false, true);

            entity.Property(e => e.ClientMessageId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ConversationId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Metadata).HasMaxLength(2000);
            entity.Property(e => e.SenderId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<MessageDetail>(entity =>
        {
            entity.HasKey(e => e.MessageId).HasName("PK__MessageD__C87C0C9C7771C499");

            entity.Property(e => e.MessageId).ValueGeneratedNever();
            entity.Property(e => e.Color)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Content).HasMaxLength(4000);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Notifica__3214EC075EC6155B");

            entity.HasIndex(e => new { e.UserId, e.IsRead, e.CreatedAt }, "IDX_Notifications_User_Read").IsDescending(false, false, true);

            entity.Property(e => e.Id)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Content).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.IsArchived).HasDefaultValue(false);
            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.ReadAt).HasColumnType("datetime");
            entity.Property(e => e.SenderId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UserId)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Post>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Posts__3214EC07A20319BE");

            entity.HasIndex(e => new { e.CreatedByUserId, e.CreatedAt }, "IDX_Posts_User_Date").IsDescending(false, true);

            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.CreatedByUserId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.GroupId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Privacy)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<PostMedium>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__PostMedi__3214EC076BA66BCA");

            entity.Property(e => e.Id)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.MediaType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.MediaUrl).HasMaxLength(500);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.ThumbnailUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<PostReaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__PostReac__3214EC07646D8617");

            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.UserId)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<PostReactionType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__PostReac__3214EC070C23116F");

            entity.Property(e => e.Name)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Users__3214EC07B81B8D6B");

            entity.HasIndex(e => e.Phone, "UQ__Users__5C7E359EC7A170D7").IsUnique();

            entity.HasIndex(e => e.Name, "UQ__Users__737584F626BC5AD9").IsUnique();

            entity.HasIndex(e => e.Email, "UQ__Users__A9D1053412836399").IsUnique();

            entity.Property(e => e.Id)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.AvatarUrl)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.DisplayName).HasMaxLength(100);
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.EmailConfirmed).HasDefaultValue(false);
            entity.Property(e => e.Gender)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PhoneConfirmed).HasDefaultValue(false);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<UserDevice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UserDevi__3214EC0727578BA1");

            entity.Property(e => e.Id)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.DeviceType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.LastUsedAt).HasColumnType("datetime");
            entity.Property(e => e.PushToken)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.UserId)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<UserFeed>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UserFeed__3214EC07CFB8C72C");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "IDX_UserFeeds_Fetch").IsDescending(false, true);

            entity.Property(e => e.Id)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.SourceType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.SourceUserId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UserId)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<UserSetting>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UserSett__3214EC07F65C13D1");

            entity.HasIndex(e => e.UserId, "IDX_UserSettings_UserId");

            entity.Property(e => e.Id)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SettingKey)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.SettingType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SettingValue).HasMaxLength(500);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
            entity.Property(e => e.UserId)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
