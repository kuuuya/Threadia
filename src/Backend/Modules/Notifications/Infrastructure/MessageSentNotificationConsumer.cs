using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Threadia.BuildingBlocks;
using Threadia.BuildingBlocks.Events;
using Threadia.Contracts.Messaging;
using Threadia.Modules.Conversations.PublicApi;
using Threadia.Modules.Identity.PublicApi;
using Threadia.Modules.Notifications.Application;
using Threadia.Modules.Notifications.Domain;
using Threadia.Modules.Presence.PublicApi;

namespace Threadia.Modules.Notifications.Infrastructure;

/// <summary>
/// message.sent を受けて通知を作成し、オフラインユーザーへ Web Push を送る Consumer。
/// - (ConsumerName, EventId) の ProcessedEvent で再処理をスキップ(冪等)
/// - NotificationId は (EventId, UserId) から決定的に導出
/// - Push 送信失敗は通知作成をロールバックしない(コミット後に送信)
/// </summary>
public sealed class MessageSentNotificationConsumer(
    NotificationsDbContext db,
    IConversationDirectory conversationDirectory,
    IConversationMembership conversationMembership,
    IUserDirectory userDirectory,
    IPresenceTracker presence,
    IWebPushSender pushSender,
    TimeProvider timeProvider,
    ILogger<MessageSentNotificationConsumer> logger) : IIntegrationEventConsumer
{
    public const string ConsumerName = "notifications";

    public string Name => ConsumerName;

    public IReadOnlyCollection<string> EventTypes { get; } = [MessagingEventTypes.MessageSent];

    public async Task HandleAsync(IntegrationEvent integrationEvent, CancellationToken ct)
    {
        if (await db.ProcessedEvents.AsNoTracking()
                .AnyAsync(p => p.ConsumerName == ConsumerName && p.EventId == integrationEvent.Id, ct))
        {
            return;
        }

        var payload = JsonSerializer.Deserialize<MessageEventPayload>(
            integrationEvent.Payload, MessagingEventTypes.SerializerOptions)
            ?? throw new InvalidOperationException("message.sent のペイロードを解釈できません。");

        var message = payload.Message;
        var conversation = await conversationDirectory.GetSummaryAsync(message.ConversationId, ct);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var notifications = new List<Notification>();
        if (conversation is not null)
        {
            var memberIds = await conversationMembership.GetActiveMemberIdsAsync(message.ConversationId, ct);
            var targets = NotificationTargets.Resolve(
                conversation.Type, memberIds, message.SenderId, message.MentionedUserIds);

            if (targets.Count > 0)
            {
                var sender = (await userDirectory.GetByIdsAsync([message.SenderId], ct)).SingleOrDefault();
                var senderName = sender?.DisplayName ?? "不明なユーザー";

                foreach (var target in targets)
                {
                    var title = target.Type == NotificationTypes.Mention
                        ? $"{senderName} さんからメンション"
                        : $"{senderName} さんからのメッセージ";

                    notifications.Add(Notification.Create(
                        DeterministicGuid.Create($"{integrationEvent.Id}:{target.UserId}"),
                        target.UserId,
                        target.Type,
                        message.ConversationId,
                        message.Id,
                        title,
                        message.Content,
                        now));
                }
            }
        }

        // 通知作成と処理済み記録を同一トランザクションで保存する。
        db.Notifications.AddRange(notifications);
        db.ProcessedEvents.Add(ProcessedEvent.Create(ConsumerName, integrationEvent.Id, now));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            // 並行して処理済み。何もしない。
            return;
        }

        // Push はコミット後に送信し、失敗しても通知処理は成功のまま(at-least-once の範囲外)。
        await SendPushAsync(notifications, ct);
    }

    private async Task SendPushAsync(IReadOnlyList<Notification> notifications, CancellationToken ct)
    {
        if (notifications.Count == 0)
        {
            return;
        }

        var userIds = notifications.Select(n => n.UserId).Distinct().ToList();
        var online = await presence.GetOnlineUsersAsync(userIds, ct);

        // オンラインユーザーは SignalR で受信するため Push は送らない。
        var offlineTargets = notifications.Where(n => !online.Contains(n.UserId)).ToList();
        if (offlineTargets.Count == 0)
        {
            return;
        }

        var subscriptions = await db.PushSubscriptions.AsNoTracking()
            .Where(s => userIds.Contains(s.UserId))
            .ToListAsync(ct);
        var subscriptionsByUser = subscriptions.ToLookup(s => s.UserId);

        var invalidSubscriptions = new List<PushSubscription>();
        foreach (var notification in offlineTargets)
        {
            var pushPayload = JsonSerializer.Serialize(new
            {
                notificationId = notification.Id,
                type = notification.Type,
                title = notification.Title,
                body = notification.Body,
                conversationId = notification.ConversationId,
            }, MessagingEventTypes.SerializerOptions);

            foreach (var subscription in subscriptionsByUser[notification.UserId])
            {
                var result = await pushSender.SendAsync(subscription, pushPayload, ct);
                if (result == PushSendResult.Gone)
                {
                    invalidSubscriptions.Add(subscription);
                }
            }
        }

        // 無効な Subscription は削除する。
        if (invalidSubscriptions.Count > 0)
        {
            var ids = invalidSubscriptions.Select(s => s.Id).Distinct().ToList();
            await db.PushSubscriptions.Where(s => ids.Contains(s.Id)).ExecuteDeleteAsync(ct);
            logger.LogInformation("Removed {Count} invalid push subscriptions", ids.Count);
        }
    }
}
