namespace Threadia.Modules.Notifications.Domain;

/// <summary>
/// Consumer が処理済みのイベント ID(CLAUDE.local.md の ProcessedMessage)。
/// メッセージが複数回配信される前提で、通知作成と同一トランザクションで記録する。
/// </summary>
public sealed class ProcessedEvent
{
    public string ConsumerName { get; private set; } = null!;
    public Guid EventId { get; private set; }
    public DateTime ProcessedAt { get; private set; }

    private ProcessedEvent()
    {
    }

    public static ProcessedEvent Create(string consumerName, Guid eventId, DateTime utcNow) =>
        new()
        {
            ConsumerName = consumerName,
            EventId = eventId,
            ProcessedAt = utcNow,
        };
}
