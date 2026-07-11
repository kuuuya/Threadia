using Microsoft.EntityFrameworkCore;
using Threadia.BuildingBlocks;
using Threadia.BuildingBlocks.Exceptions;
using Threadia.Modules.Notifications.Domain;
using Threadia.Modules.Notifications.Infrastructure;

namespace Threadia.Modules.Notifications.Application;

public sealed record NotificationDto(
    Guid Id, string Type, Guid ConversationId, Guid MessageId, string Title, string Body, DateTime CreatedAt, DateTime? ReadAt);

public sealed class NotificationService(NotificationsDbContext db, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<NotificationDto>> GetMineAsync(Guid userId, int? limit, CancellationToken ct)
    {
        return await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(Paging.ClampLimit(limit))
            .Select(n => new NotificationDto(n.Id, n.Type, n.ConversationId, n.MessageId, n.Title, n.Body, n.CreatedAt, n.ReadAt))
            .ToListAsync(ct);
    }

    public async Task MarkReadAsync(Guid notificationId, Guid userId, CancellationToken ct)
    {
        var notification = await db.Notifications
            .SingleOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct)
            ?? throw new NotFoundException("通知が見つかりません。");

        notification.MarkRead(timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
    }
}
