using Microsoft.EntityFrameworkCore;
using Threadia.Modules.Notifications.Domain;

namespace Threadia.Modules.Notifications.Infrastructure;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    public const string Schema = "notifications";

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Notification>(notification =>
        {
            notification.HasKey(n => n.Id);
            notification.Property(n => n.Type).HasMaxLength(30).IsRequired();
            notification.Property(n => n.Title).HasMaxLength(Notification.MaxTitleLength).IsRequired();
            notification.Property(n => n.Body).HasMaxLength(Notification.MaxBodyLength).IsRequired();
            notification.HasIndex(n => new { n.UserId, n.CreatedAt });
        });

        modelBuilder.Entity<PushSubscription>(subscription =>
        {
            subscription.HasKey(s => s.Id);
            subscription.Property(s => s.Endpoint).HasMaxLength(PushSubscription.MaxEndpointLength).IsRequired();
            subscription.Property(s => s.P256dh).HasMaxLength(300).IsRequired();
            subscription.Property(s => s.Auth).HasMaxLength(100).IsRequired();
            subscription.HasIndex(s => s.Endpoint).IsUnique();
            subscription.HasIndex(s => s.UserId);
        });

        modelBuilder.Entity<ProcessedEvent>(processed =>
        {
            processed.HasKey(p => new { p.ConsumerName, p.EventId });
            processed.Property(p => p.ConsumerName).HasMaxLength(100);
        });
    }
}
