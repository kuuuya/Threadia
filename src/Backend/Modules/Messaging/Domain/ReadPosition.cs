namespace Threadia.Modules.Messaging.Domain;

/// <summary>
/// ユーザーが会話内で最後に読んだ位置。Message 単位の既読レコードは作成しない。
/// </summary>
public sealed class ReadPosition
{
    public Guid UserId { get; private set; }
    public Guid ConversationId { get; private set; }
    public long LastReadSequence { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private ReadPosition()
    {
    }

    public static ReadPosition Create(Guid userId, Guid conversationId, long lastReadSequence, DateTime utcNow)
    {
        if (lastReadSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lastReadSequence), "既読位置は0以上を指定してください。");
        }

        return new ReadPosition
        {
            UserId = userId,
            ConversationId = conversationId,
            LastReadSequence = lastReadSequence,
            UpdatedAt = utcNow,
        };
    }

    /// <summary>既読位置を前進させる。後退させる要求は無視する(LastReadSequence は後退しない)。</summary>
    public void Advance(long lastReadSequence, DateTime utcNow)
    {
        if (lastReadSequence > LastReadSequence)
        {
            LastReadSequence = lastReadSequence;
            UpdatedAt = utcNow;
        }
    }
}
