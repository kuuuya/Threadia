namespace Threadia.Modules.Messaging.Domain;

/// <summary>
/// メンション対象。表示文字列ではなく API で明示された UserId を保存する。
/// </summary>
public sealed class MessageMention
{
    public Guid MessageId { get; private set; }
    public Guid MentionedUserId { get; private set; }

    private MessageMention()
    {
    }

    public static MessageMention Create(Guid messageId, Guid mentionedUserId) =>
        new()
        {
            MessageId = messageId,
            MentionedUserId = mentionedUserId,
        };
}
