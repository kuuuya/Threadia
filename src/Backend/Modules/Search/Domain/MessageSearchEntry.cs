namespace Threadia.Modules.Search.Domain;

/// <summary>
/// 検索用の派生データ。正本は Messaging の Messages であり、Outbox イベントから再構築できる。
/// 検索サービスを正本にしない(ADR 0006)。
/// </summary>
public sealed class MessageSearchEntry
{
    public Guid MessageId { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid SenderId { get; private set; }
    public long Sequence { get; private set; }
    public string Content { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    private MessageSearchEntry()
    {
    }

    public static MessageSearchEntry Create(
        Guid messageId, Guid conversationId, Guid workspaceId, Guid senderId, long sequence, string content, DateTime createdAt) =>
        new()
        {
            MessageId = messageId,
            ConversationId = conversationId,
            WorkspaceId = workspaceId,
            SenderId = senderId,
            Sequence = sequence,
            Content = content,
            CreatedAt = createdAt,
        };

    public void UpdateContent(string content)
    {
        Content = content;
    }
}
