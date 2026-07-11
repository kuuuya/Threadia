using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Threadia.BuildingBlocks.Events;
using Threadia.Contracts.Messaging;
using Threadia.Modules.Attachments.Application;

namespace Threadia.Modules.Attachments.Infrastructure;

/// <summary>
/// Message 削除イベントを受けて添付ファイルを非同期に削除する Consumer。
/// 削除は自然に冪等(同じイベントを再処理しても結果は同じ)。
/// </summary>
public sealed class MessageDeletedAttachmentCleanupConsumer(
    AttachmentsDbContext db,
    IObjectStorage storage,
    ILogger<MessageDeletedAttachmentCleanupConsumer> logger) : IIntegrationEventConsumer
{
    public string Name => "attachments-cleanup";

    public IReadOnlyCollection<string> EventTypes { get; } = [MessagingEventTypes.MessageDeleted];

    public async Task HandleAsync(IntegrationEvent integrationEvent, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<MessageDeletedPayload>(
            integrationEvent.Payload, MessagingEventTypes.SerializerOptions)
            ?? throw new InvalidOperationException("message.deleted のペイロードを解釈できません。");

        var attachments = await db.Attachments
            .Where(a => a.MessageId == payload.MessageId)
            .ToListAsync(ct);

        foreach (var attachment in attachments)
        {
            await storage.DeleteAsync(attachment.StorageKey, ct);
            db.Attachments.Remove(attachment);
        }

        if (attachments.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Deleted {Count} attachments for message {MessageId}", attachments.Count, payload.MessageId);
        }
    }
}
