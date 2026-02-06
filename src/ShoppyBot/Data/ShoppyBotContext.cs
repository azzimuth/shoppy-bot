using Microsoft.EntityFrameworkCore;
using ShoppyBot.Models;

namespace ShoppyBot.Data;

public class ShoppyBotContext : DbContext
{
    public ShoppyBotContext(DbContextOptions<ShoppyBotContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<ShoppingList> ShoppingLists => Set<ShoppingList>();
    public DbSet<ListItem> ListItems => Set<ListItem>();
    public DbSet<UserListAccess> UserListAccess => Set<UserListAccess>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TelegramId).IsUnique();
            entity.Property(e => e.TelegramId).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(255);
            entity.Property(e => e.Username).HasMaxLength(255);

            entity.HasOne(e => e.CurrentList)
                .WithMany()
                .HasForeignKey(e => e.CurrentListId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ShoppingList>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ShareToken).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => e.ShareToken).IsUnique();

            entity.HasOne(e => e.Creator)
                .WithMany(u => u.CreatedLists)
                .HasForeignKey(e => e.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ListItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ItemName).HasMaxLength(500).IsRequired();

            entity.HasOne(e => e.List)
                .WithMany(l => l.Items)
                .HasForeignKey(e => e.ListId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.AddedBy)
                .WithMany(u => u.AddedItems)
                .HasForeignKey(e => e.AddedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.ListId, e.OrderIndex });
        });

        modelBuilder.Entity<UserListAccess>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.ListId }).IsUnique();

            entity.HasOne(e => e.User)
                .WithMany(u => u.ListAccess)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.List)
                .WithMany(l => l.UserAccess)
                .HasForeignKey(e => e.ListId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Details).HasMaxLength(1000);

            entity.HasOne(e => e.List)
                .WithMany(l => l.ActivityLogs)
                .HasForeignKey(e => e.ListId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany(u => u.ActivityLogs)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.ListId, e.CreatedAt });
        });
    }
}
