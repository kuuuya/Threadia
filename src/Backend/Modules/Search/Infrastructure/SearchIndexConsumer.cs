using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Threadia.BuildingBlocks.Events;
using Threadia.Contracts.Messaging;
using Threadia.Modules.Conversations.PublicApi;
using Threadia.Modules.Search.Domain;

namespace Threadia.Modules.Search.Infrastructure;

/// <summary>
/// Messaging イベントから検索インデックスを更新する Consumer。
/// MessageId をキーとした UPSERT / DELETE のため自然に冪等で、再配信されても結果は変わらない。
/// </summary>
public sealed class SearchIndexConsumer(
    SearchDbContext db,
    IConversationDirectory conversationDirectory) : IIntegrationEventConsumer
{
    public string Name => "search-index";

    public IReadOnlyCollection<string> EventTypes { get; } =
    [
        MessagingEventTypes.MessageSent,
        MessagingEventTypes.MessageEdited,
        MessagingEventTypes.MessageDeleted,
    ];

    public async Task HandleAsync(IntegrationEvent integrationEvent, CancellationToken ct)
    {
        if (integrationEvent.Type == MessagingEventTypes.MessageDeleted)
        {
            var deleted = Deserialize<MessageDeletedPayload>(integrationEvent.Payload);
            await db.Entries.Where(e => e.MessageId == deleted.MessageId).ExecuteDeleteAsync(ct);
            return;
        }

        var payload = Deserialize<MessageEventPayload>(integrationEvent.Payload);
        var message = payload.Message;

        // 削除済みメッセージはインデックスに残さない。
        if (message.IsDeleted)
        {
            await db.Entries.Where(e => e.MessageId == message.Id).ExecuteDeleteAsync(ct);
            return;
        }

        var existing = await db.Entries.SingleOrDefaultAsync(e => e.MessageId == message.Id, ct);
        if (existing is not null)
        {
            existing.UpdateContent(message.Content);
            await db.SaveChangesAsync(ct);
            return;
        }

        var conversation = await conversationDirectory.GetSummaryAsync(message.ConversationId, ct);
        if (conversation is null)
        {
            return;
        }

        db.Entries.Add(MessageSearchEntry.Create(
            message.Id, message.ConversationId, conversation.WorkspaceId,
            message.SenderId, message.Sequence, message.Content, message.CreatedAt));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            // 並行して登録済み(再配信)。何もしない。
        }
    }

    private static T Deserialize<T>(string payload) =>
        JsonSerializer.Deserialize<T>(payload, MessagingEventTypes.SerializerOptions)
        ?? throw new InvalidOperationException($"{typeof(T).Name} のペイロードを解釈できません。");
}
