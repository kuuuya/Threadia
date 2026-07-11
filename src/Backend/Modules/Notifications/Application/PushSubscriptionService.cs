using Microsoft.EntityFrameworkCore;
using Threadia.BuildingBlocks;
using Threadia.BuildingBlocks.Exceptions;
using Threadia.Modules.Notifications.Domain;
using Threadia.Modules.Notifications.Infrastructure;

namespace Threadia.Modules.Notifications.Application;

public sealed class PushSubscriptionService(NotificationsDbContext db, TimeProvider timeProvider)
{
    /// <summary>同じ endpoint の再購読は上書きする(冪等)。</summary>
    public async Task SubscribeAsync(Guid userId, string endpoint, string p256dh, string auth, CancellationToken ct)
    {
        PushSubscription subscription;
        try
        {
            subscription = PushSubscription.Create(Ids.New(), userId, endpoint, p256dh, auth, timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException(ex.Message);
        }

        var existing = await db.PushSubscriptions.SingleOrDefaultAsync(s => s.Endpoint == endpoint, ct);
        if (existing is not null)
        {
            db.PushSubscriptions.Remove(existing);
        }

        db.PushSubscriptions.Add(subscription);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            // 並行購読は冪等に成功として扱う。
        }
    }

    public async Task UnsubscribeAsync(Guid userId, string endpoint, CancellationToken ct)
    {
        var existing = await db.PushSubscriptions
            .SingleOrDefaultAsync(s => s.Endpoint == endpoint && s.UserId == userId, ct);

        if (existing is not null)
        {
            db.PushSubscriptions.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
    }
}
