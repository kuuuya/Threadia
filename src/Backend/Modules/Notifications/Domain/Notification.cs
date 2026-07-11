namespace Threadia.Modules.Notifications.Domain;

public static class NotificationTypes
{
    public const string DirectMessage = "direct_message";
    public const string Mention = "mention";
}

public sealed class Notification
{
    public const int MaxTitleLength = 100;
    public const int MaxBodyLength = 200;

    /// <summary>
    /// (EventId, UserId) から決定的に導出する冪等性キー。
    /// 同じイベントを再処理しても同じ ID になり、重複作成は主キー制約で防がれる。
    /// クライアントもこの ID で重複表示を防ぐ。
    /// </summary>
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }
    public string Type { get; private set; } = null!;
    public Guid ConversationId { get; private set; }
    public Guid MessageId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Body { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReadAt { get; private set; }

    private Notification()
    {
    }

    public static Notification Create(
        Guid id, Guid userId, string type, Guid conversationId, Guid messageId, string title, string body, DateTime utcNow) =>
        new()
        {
            Id = id,
            UserId = userId,
            Type = type,
            ConversationId = conversationId,
            MessageId = messageId,
            Title = Truncate(title, MaxTitleLength),
            Body = Truncate(body, MaxBodyLength),
            CreatedAt = utcNow,
        };

    public void MarkRead(DateTime utcNow)
    {
        ReadAt ??= utcNow;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
