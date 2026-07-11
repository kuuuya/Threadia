namespace Threadia.Modules.Messaging.Domain;

/// <summary>
/// Outbox Pattern のイベントレコード。Message 保存と同一トランザクションで書き込み、
/// Outbox Worker が SignalR 配信などの副作用を非同期に実行する。
/// </summary>
public sealed class OutboxMessage
{
    public const int MaxAttempts = 5;

    public Guid Id { get; private set; }

    /// <summary>イベント種別(例: message.sent / message.edited / message.deleted)。</summary>
    public string Type { get; private set; } = null!;

    /// <summary>イベント本文(JSON)。conversationId プロパティを必ず含める。</summary>
    public string Payload { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? NextAttemptAt { get; private set; }

    /// <summary>リトライ上限超過。RabbitMQ 導入までは、このフラグ付き行が Dead Letter Queue の役割を持つ。</summary>
    public DateTime? DeadLetteredAt { get; private set; }

    private OutboxMessage()
    {
    }

    public static OutboxMessage Create(Guid id, string type, string payload, DateTime utcNow) =>
        new()
        {
            Id = id,
            Type = type,
            Payload = payload,
            CreatedAt = utcNow,
        };

    public void MarkProcessed(DateTime utcNow)
    {
        ProcessedAt = utcNow;
    }

    public void MarkFailed(string error, DateTime utcNow)
    {
        Attempts++;
        LastError = error.Length > 2000 ? error[..2000] : error;

        if (Attempts >= MaxAttempts)
        {
            DeadLetteredAt = utcNow;
            return;
        }

        // 指数バックオフ(2, 4, 8, 16 秒)。
        NextAttemptAt = utcNow.AddSeconds(Math.Pow(2, Attempts));
    }
}
