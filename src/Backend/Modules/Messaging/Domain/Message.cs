namespace Threadia.Modules.Messaging.Domain;

public sealed class Message
{
    public const int MaxContentLength = 4000;
    public const int MaxClientMessageIdLength = 64;

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }

    /// <summary>Conversation 内で単調増加する順序番号。編集しても変更しない。</summary>
    public long Sequence { get; private set; }

    public Guid SenderId { get; private set; }

    /// <summary>クライアントが生成する再送重複防止キー。(SenderId, ClientMessageId) で UNIQUE。</summary>
    public string ClientMessageId { get; private set; } = null!;

    public string Content { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? EditedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private Message()
    {
    }

    public static Message Create(
        Guid id, Guid conversationId, long sequence, Guid senderId, string clientMessageId, string content, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length > MaxContentLength)
        {
            throw new ArgumentException($"本文は1〜{MaxContentLength}文字で指定してください。", nameof(content));
        }

        if (string.IsNullOrWhiteSpace(clientMessageId) || clientMessageId.Length > MaxClientMessageIdLength)
        {
            throw new ArgumentException($"ClientMessageId は1〜{MaxClientMessageIdLength}文字で指定してください。", nameof(clientMessageId));
        }

        return new Message
        {
            Id = id,
            ConversationId = conversationId,
            Sequence = sequence,
            SenderId = senderId,
            ClientMessageId = clientMessageId,
            Content = content,
            CreatedAt = utcNow,
        };
    }

    public bool IsDeleted => DeletedAt is not null;

    public void Edit(string content, DateTime utcNow)
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("削除済みのメッセージは編集できません。");
        }

        if (string.IsNullOrWhiteSpace(content) || content.Length > MaxContentLength)
        {
            throw new ArgumentException($"本文は1〜{MaxContentLength}文字で指定してください。", nameof(content));
        }

        Content = content;
        EditedAt = utcNow;
    }

    public void Delete(DateTime utcNow)
    {
        if (!IsDeleted)
        {
            DeletedAt = utcNow;
        }
    }
}
