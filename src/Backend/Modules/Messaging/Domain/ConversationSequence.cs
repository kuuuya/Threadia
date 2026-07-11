namespace Threadia.Modules.Messaging.Domain;

/// <summary>
/// Conversation ごとの採番カウンタ。UPSERT + 行ロックで単調増加を保証する。
/// 性能問題が実測されるまで分散採番器は導入しない(ADR 0001)。
/// </summary>
public sealed class ConversationSequence
{
    public Guid ConversationId { get; private set; }
    public long LastSequence { get; private set; }

    private ConversationSequence()
    {
    }
}
