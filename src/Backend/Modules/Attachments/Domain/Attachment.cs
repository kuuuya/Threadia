namespace Threadia.Modules.Attachments.Domain;

public sealed class Attachment
{
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid UploaderId { get; private set; }
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;

    /// <summary>クライアント申告サイズ(バイト)。実サイズは Message 関連付け時にストレージで検証する。</summary>
    public long Size { get; private set; }

    /// <summary>オブジェクトストレージ上のキー。</summary>
    public string StorageKey { get; private set; } = null!;

    /// <summary>関連付けられた Message。null はアップロード中(未関連付け)で、孤立掃除の対象。</summary>
    public Guid? MessageId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private Attachment()
    {
    }

    public static Attachment Create(
        Guid id, Guid conversationId, Guid uploaderId, string fileName, string contentType, long size, DateTime utcNow)
    {
        var sanitized = AttachmentPolicy.Validate(fileName, contentType, size);

        return new Attachment
        {
            Id = id,
            ConversationId = conversationId,
            UploaderId = uploaderId,
            FileName = sanitized,
            ContentType = contentType,
            Size = size,
            StorageKey = $"{conversationId:N}/{id:N}/{sanitized}",
            CreatedAt = utcNow,
        };
    }

    public bool IsBound => MessageId is not null;

    public void BindToMessage(Guid messageId)
    {
        if (MessageId is not null && MessageId != messageId)
        {
            throw new InvalidOperationException("この添付ファイルは既に別のメッセージへ関連付けられています。");
        }

        MessageId = messageId;
    }
}
