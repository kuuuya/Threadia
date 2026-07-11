namespace Threadia.Modules.Notifications.Application;

public sealed record NotificationTarget(Guid UserId, string Type);

/// <summary>
/// 通知対象の決定(純粋ロジック)。
/// - メンションされた参加者(送信者以外)→ mention
/// - Direct Conversation の相手 → direct_message(メンションが優先)
/// 会話ごとの通知設定(有効化した Conversation)は未実装の将来拡張。
/// </summary>
public static class NotificationTargets
{
    public static IReadOnlyList<NotificationTarget> Resolve(
        string conversationType,
        IReadOnlyCollection<Guid> activeMemberIds,
        Guid senderId,
        IReadOnlyCollection<Guid> mentionedUserIds)
    {
        var targets = new Dictionary<Guid, string>();

        if (conversationType == "Direct")
        {
            foreach (var memberId in activeMemberIds.Where(id => id != senderId))
            {
                targets[memberId] = Domain.NotificationTypes.DirectMessage;
            }
        }

        // メンションは参加中のメンバーのみ対象とし、direct_message より優先する。
        foreach (var mentionedId in mentionedUserIds.Where(id => id != senderId && activeMemberIds.Contains(id)))
        {
            targets[mentionedId] = Domain.NotificationTypes.Mention;
        }

        return targets.Select(kv => new NotificationTarget(kv.Key, kv.Value)).ToList();
    }
}
