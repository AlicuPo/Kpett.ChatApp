using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Kpett.ChatApp.Entities;

public partial class KpettChatAppContext : DbContext
{
    public KpettChatAppContext()
    {
    }

    public KpettChatAppContext(DbContextOptions<KpettChatAppContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Conversation> Conversations { get; set; }

    public virtual DbSet<ConversationMember> ConversationMembers { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<MessageRead> MessageReads { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Server=.\\SQLEXPRESS;Database=Kpett_ChatApp;Trusted_Connection=True;TrustServerCertificate=True;Integrated Security=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Conversa__3214EC070961678B");

            entity.HasIndex(e => e.LastMessageAt, "IX_Conversations_LastMessageAt").IsDescending();

            entity.HasIndex(e => e.ConversationType, "IX_Conversations_Type");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.ConversationName).HasMaxLength(200);
            entity.Property(e => e.ConversationType).HasDefaultValue((byte)1);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.LastMessageAt).HasColumnType("datetime");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Conversations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Conversat__Creat__5BE2A6F2");
        });

        modelBuilder.Entity<ConversationMember>(entity =>
        {
            entity.HasKey(e => new { e.ConversationId, e.UserId }).HasName("PK__Conversa__112854B3873BDD64");

            entity.HasIndex(e => e.LastSeenAt, "IX_ConversationMembers_LastSeenAt");

            entity.HasIndex(e => e.UserId, "IX_ConversationMembers_UserId");

            entity.Property(e => e.IsAdmin).HasDefaultValue(false);
            entity.Property(e => e.IsMuted).HasDefaultValue(false);
            entity.Property(e => e.JoinedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.LastSeenAt).HasColumnType("datetime");
            entity.Property(e => e.Nickname).HasMaxLength(100);

            entity.HasOne(d => d.Conversation).WithMany(p => p.ConversationMembers)
                .HasForeignKey(d => d.ConversationId)
                .HasConstraintName("FK__Conversat__Conve__5FB337D6");

            entity.HasOne(d => d.User).WithMany(p => p.ConversationMembers)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__Conversat__UserI__60A75C0F");
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Messages__3214EC07A4C43F07");

            entity.HasIndex(e => e.ConversationId, "IX_Messages_ConversationId");

            entity.HasIndex(e => new { e.ConversationId, e.CreatedAt }, "IX_Messages_ConversationId_CreatedAt").IsDescending(false, true);

            entity.HasIndex(e => e.CreatedAt, "IX_Messages_CreatedAt").IsDescending();

            entity.HasIndex(e => e.SenderId, "IX_Messages_SenderId");

            entity.Property(e => e.AttachmentUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.DeletedForAll).HasDefaultValue(false);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.IsEdited).HasDefaultValue(false);
            entity.Property(e => e.MessageType).HasDefaultValue((byte)1);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.Conversation).WithMany(p => p.Messages)
                .HasForeignKey(d => d.ConversationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK__Messages__Conver__66603565");

            entity.HasOne(d => d.Sender).WithMany(p => p.Messages)
                .HasForeignKey(d => d.SenderId)
                .HasConstraintName("FK__Messages__Sender__6754599E");
        });

        modelBuilder.Entity<MessageRead>(entity =>
        {
            entity.HasKey(e => new { e.MessageId, e.UserId }).HasName("PK__MessageR__19048058A324B5C7");

            entity.HasIndex(e => new { e.UserId, e.ConversationId }, "IX_MessageReads_UserId_ConversationId");

            entity.Property(e => e.ReadAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Conversation).WithMany(p => p.MessageReads)
                .HasForeignKey(d => d.ConversationId)
                .HasConstraintName("FK__MessageRe__Conve__70DDC3D8");

            entity.HasOne(d => d.Message).WithMany(p => p.MessageReads)
                .HasForeignKey(d => d.MessageId)
                .HasConstraintName("FK__MessageRe__Messa__6EF57B66");

            entity.HasOne(d => d.User).WithMany(p => p.MessageReads)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__MessageRe__UserI__6FE99F9F");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__RefreshT__3214EC07C97AE41C");

            entity.HasIndex(e => e.ExpiresAt, "IX_RefreshTokens_ExpiresAt");

            entity.HasIndex(e => e.Token, "IX_RefreshTokens_Token");

            entity.HasIndex(e => e.UserId, "IX_RefreshTokens_UserId");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ExpiresAt).HasColumnType("datetime");
            entity.Property(e => e.ReplacedByToken).HasMaxLength(500);
            entity.Property(e => e.RevokedAt).HasColumnType("datetime");
            entity.Property(e => e.Token).HasMaxLength(500);

            entity.HasOne(d => d.User).WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK__RefreshTo__UserI__797309D9");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Roles__3214EC07F690640C");

            entity.HasIndex(e => e.Name, "UQ__Roles__737584F63FAE200E").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Users__3214EC0705ACA794");

            entity.HasIndex(e => e.Email, "IX_Users_Email");

            entity.HasIndex(e => e.IsActive, "IX_Users_IsActive");

            entity.HasIndex(e => e.Username, "IX_Users_Username");

            entity.HasIndex(e => e.Username, "UQ__Users__536C85E4588EB6F7").IsUnique();

            entity.HasIndex(e => e.Email, "UQ__Users__A9D105341E1EFB89").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.DeletedAt).HasColumnType("datetime");
            entity.Property(e => e.DisplayName).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsMuted).HasDefaultValue(false);
            entity.Property(e => e.LastActiveAt).HasColumnType("datetime");
            entity.Property(e => e.PasswordHash).HasMaxLength(256);
            entity.Property(e => e.PasswordSalt).HasMaxLength(128);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
            entity.Property(e => e.Username).HasMaxLength(50);
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RoleId }).HasName("PK__UserRole__AF2760ADCA31DA69");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("FK__UserRoles__RoleI__5629CD9C");

            entity.HasOne(d => d.User).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__UserRoles__UserI__5535A963");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
