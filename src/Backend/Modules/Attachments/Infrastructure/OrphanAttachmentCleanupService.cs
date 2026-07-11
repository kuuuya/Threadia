using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Threadia.Modules.Attachments.Application;

namespace Threadia.Modules.Attachments.Infrastructure;

/// <summary>
/// Message へ関連付けられないまま残った Attachment(アップロード放置・送信失敗)を定期削除する。
/// Workers ホストで動かす。
/// </summary>
public sealed class OrphanAttachmentCleanupService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<OrphanAttachmentCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private static readonly TimeSpan OrphanAge = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Orphan attachment cleanup failed");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AttachmentsDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();

        var threshold = timeProvider.GetUtcNow().UtcDateTime.Subtract(OrphanAge);
        var orphans = await db.Attachments
            .Where(a => a.MessageId == null && a.CreatedAt < threshold)
            .Take(100)
            .ToListAsync(ct);

        foreach (var orphan in orphans)
        {
            await storage.DeleteAsync(orphan.StorageKey, ct);
            db.Attachments.Remove(orphan);
        }

        if (orphans.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Deleted {Count} orphan attachments", orphans.Count);
        }
    }
}
