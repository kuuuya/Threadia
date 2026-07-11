namespace Threadia.Modules.Conversations.Domain;

public sealed class ConversationMember
{
    public Guid ConversationId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime JoinedAt { get; private set; }

    /// <summary>退出時刻。null なら参加中。参照可否の判定は LeftAt == null を条件とする。</summary>
    public DateTime? LeftAt { get; private set; }

    private ConversationMember()
    {
    }

    public static ConversationMember Create(Guid conversationId, Guid userId, DateTime utcNow) =>
        new()
        {
            ConversationId = conversationId,
            UserId = userId,
            JoinedAt = utcNow,
        };

    public bool IsActive => LeftAt is null;

    public void Leave(DateTime utcNow)
    {
        if (LeftAt is null)
        {
            LeftAt = utcNow;
        }
    }

    /// <summary>再参加。JoinedAt は初回参加時刻を保持する。</summary>
    public void Rejoin()
    {
        LeftAt = null;
    }
}
