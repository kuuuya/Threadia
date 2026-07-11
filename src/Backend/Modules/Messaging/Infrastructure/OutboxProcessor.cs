using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Threadia.BuildingBlocks.Events;
using Threadia.BuildingBlocks.Telemetry;
using Threadia.Modules.Messaging.Domain;

namespace Threadia.Modules.Messaging.Infrastructure;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int PollingIntervalMs { get; set; } = 500;
    public int BatchSize { get; set; } = 20;
}

/// <summary>
/// Outbox テーブルをポーリングし、未処理イベントを配信するワーカー。
/// FOR UPDATE SKIP LOCKED により複数インスタンスでも同じ行を二重処理しない。
/// 配信失敗はリトライし、上限超過で Dead Letter 状態にする。
/// </summary>
public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                processed = await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox batch processing failed");
            }

            if (processed == 0)
            {
                try
                {
                    await Task.Delay(options.Value.PollingIntervalMs, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        logger.LogInformation("Outbox processor stopped");
    }

    private async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MessagingDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxEventDispatcher>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IIntegrationEventBus>();

        var now = timeProvider.GetUtcNow().UtcDateTime;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var due = await db.OutboxMessages.FromSqlRaw("""
            SELECT * FROM messaging."OutboxMessages"
            WHERE "ProcessedAt" IS NULL
              AND "DeadLetteredAt" IS NULL
              AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= {0})
            ORDER BY "CreatedAt"
            LIMIT {1}
            FOR UPDATE SKIP LOCKED
            """, now, options.Value.BatchSize).ToListAsync(ct);

        if (due.Count == 0)
        {
            await tx.RollbackAsync(ct);
            return 0;
        }

        foreach (var outboxMessage in due)
        {
            try
            {
                // SignalR 配信とイベントバス発行の両方が成功した場合のみ処理済みにする。
                // 片方成功後の失敗は再実行で重複配信になるが、受信側は Id で冪等に処理する(at-least-once)。
                await dispatcher.DispatchAsync(outboxMessage.Type, outboxMessage.Payload, ct);
                await eventBus.PublishAsync(
                    new IntegrationEvent(outboxMessage.Id, outboxMessage.Type, outboxMessage.Payload, outboxMessage.CreatedAt),
                    ct);
                outboxMessage.MarkProcessed(timeProvider.GetUtcNow().UtcDateTime);
                ThreadiaDiagnostics.OutboxProcessed.Add(1);
            }
            catch (Exception ex)
            {
                outboxMessage.MarkFailed(ex.Message, timeProvider.GetUtcNow().UtcDateTime);
                ThreadiaDiagnostics.OutboxFailed.Add(1);
                if (outboxMessage.DeadLetteredAt is not null)
                {
                    ThreadiaDiagnostics.OutboxDeadLettered.Add(1);
                }

                logger.LogWarning(ex,
                    "Outbox dispatch failed. OutboxMessageId={OutboxMessageId} Type={Type} Attempts={Attempts}",
                    outboxMessage.Id, outboxMessage.Type, outboxMessage.Attempts);
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return due.Count;
    }
}
